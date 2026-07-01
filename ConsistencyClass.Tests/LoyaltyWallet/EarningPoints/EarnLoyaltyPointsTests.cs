using ConsistencyClass.LoyaltyWallets;
using ConsistencyClass.LoyaltyWallets.EarningPoints;
using ConsistencyClass.LoyaltyWallets.WalletLifecycle;
using ConsistencyClass.Membership;
using ConsistencyClass.Membership.MemberDirectory;

namespace ConsistencyClass.Tests.LoyaltyWallets.EarningPoints;

public class EarnLoyaltyPointsTests
{
    private static readonly MemberId Buyer = MemberId.Random();
    private static readonly MemberId Referrer = MemberId.Random();
    private static readonly MemberId Operator = MemberId.Random();

    private readonly DatabaseCollection<Member> _members = Database.Collection<Member>();
    private readonly LoyaltyWalletStore _store = new(Database.Collection<WalletDocument>());
    private readonly MemberTierReader _tierReader;
    private readonly EarnLoyaltyPointsHandler _earnHandler;

    public EarnLoyaltyPointsTests()
    {
        _tierReader = new MemberTierReader(_members);
        _earnHandler = new EarnLoyaltyPointsHandler(
            _store.FindLoyaltyWalletsByOwners,
            _store.SaveLoyaltyWallets,
            _tierReader.GetTier);
    }

    private async Task Enroll(MemberId memberId, Tier tier = Tier.Standard)
    {
        var @event = new MemberVerified(memberId, tier);
        await new MemberVerifiedHandler(_members).Handle(@event);
        await new OpenWalletOnMemberVerifiedHandler(_store.SaveLoyaltyWallet).Handle(@event);
    }

    private Task Earn(PurchaseRecorded purchase) => _earnHandler.Handle(purchase).AsTask();

    private PurchaseRecorded Purchase(
        MemberId buyer,
        Channel channel = Channel.InStore) =>
        new(
            Money.Of(1000),
            buyer,
            channel,
            new DateTime(2026, 6, 23, 12, 0, 0),
            [],
            [new Beneficiary(Referrer, EarnRole.Referrer), new Beneficiary(Operator, EarnRole.Operator)]);

    private async Task<int> AvailableOf(MemberId memberId)
    {
        var wallets = await _store.FindLoyaltyWalletsByOwners([memberId]);
        var wallet = wallets.ShouldHaveSingleItem().ShouldBeOfType<LoyaltyWallet.Active>();
        return wallet.PointsLimit.AvailablePoints.Value;
    }

    private async Task<bool> HasWallet(MemberId memberId)
    {
        var wallets = await _store.FindLoyaltyWalletsByOwners([memberId]);
        return wallets.Count > 0 && wallets[0] is LoyaltyWallet.Active;
    }

    private async Task Deactivate(MemberId memberId)
    {
        var wallets = await _store.FindLoyaltyWalletsByOwners([memberId]);
        var wallet = wallets.ShouldHaveSingleItem().ShouldBeOfType<LoyaltyWallet.Active>();
        await new DeactivateWalletHandler(_store.GetLoyaltyWallet, _store.SaveLoyaltyWallet)
            .Handle(new LoyaltyWalletCommand.DeactivateWallet(wallet.WalletNumber));
    }

    [Fact]
    public async Task DistributesPointsAcrossTheBuyerAndEveryBeneficiary()
    {
        // given
        await Enroll(Buyer);
        await Enroll(Referrer);
        await Enroll(Operator);

        // when
        await Earn(Purchase(Buyer));

        // then a Standard, in-store, daytime purchase of 1000 earns 100 to the buyer
        (await AvailableOf(Buyer)).ShouldBe(100);
        // and each beneficiary earns its own share of the buyer's points
        (await AvailableOf(Referrer)).ShouldBe(10);
        (await AvailableOf(Operator)).ShouldBe(5);
    }

    [Fact]
    public async Task AppliesBuyersTierToBuyersShare()
    {
        // given a Gold buyer
        await Enroll(Buyer, Tier.Gold);
        await Enroll(Referrer);
        await Enroll(Operator);

        // when
        await Earn(Purchase(Buyer));

        // then the Gold multiplier lifts the buyer's base to 125
        (await AvailableOf(Buyer)).ShouldBe(125);
    }

    [Fact]
    public async Task SkipsABeneficiaryOutsideTheLoyaltyProgram()
    {
        // given the operator is not enrolled, so has no wallet
        await Enroll(Buyer);
        await Enroll(Referrer);

        // when
        await Earn(Purchase(Buyer));

        // then the enrolled recipients are still credited
        (await AvailableOf(Buyer)).ShouldBe(100);
        (await AvailableOf(Referrer)).ShouldBe(10);
        // and the unenrolled operator is simply skipped, no wallet conjured
        (await HasWallet(Operator)).ShouldBeFalse();
    }

    [Fact]
    public async Task SkipsABeneficiaryWhoseWalletIsDeactivated()
    {
        // given the referrer's wallet is deactivated
        await Enroll(Buyer);
        await Enroll(Referrer);
        await Enroll(Operator);
        await Deactivate(Referrer);

        // when
        await Earn(Purchase(Buyer));

        // then the active recipients are still credited
        (await AvailableOf(Buyer)).ShouldBe(100);
        (await AvailableOf(Operator)).ShouldBe(5);
        // and the deactivated referrer is skipped, its balance untouched
        var referrer = (await _store.FindLoyaltyWalletsByOwners([Referrer]))[0];
        referrer.ShouldBeOfType<LoyaltyWallet.Deactivated>()
            .PointsLimit.AvailablePoints.Value.ShouldBe(0);
    }

    [Fact]
    public async Task CantEarnForAnUnknownBuyer()
    {
        // given the buyer was never verified
        await Enroll(Referrer);
        await Enroll(Operator);

        // when
        await Should.ThrowAsync<InvalidOperationException>(() => Earn(Purchase(Buyer)));
    }
}
