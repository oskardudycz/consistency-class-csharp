using ConsistencyClass.LoyaltyWallets;
using ConsistencyClass.LoyaltyWallets.RedemptionWindows;
using ConsistencyClass.LoyaltyWallets.RedemptionWindows.EarningPoints;
using ConsistencyClass.LoyaltyWallets.RedemptionWindows.WindowLifecycle;
using ConsistencyClass.LoyaltyWallets.WalletLifecycle;
using ConsistencyClass.Membership;
using ConsistencyClass.Membership.MemberDirectory;
using static ConsistencyClass.LoyaltyWallets.RedemptionWindows.RedemptionWindowDecider;

namespace ConsistencyClass.Tests.LoyaltyWallets.RedemptionWindows.EarningPoints;

public class EarnLoyaltyPointsTests
{
    private static readonly MemberId Buyer = MemberId.Random();
    private static readonly MemberId Referrer = MemberId.Random();
    private static readonly MemberId Operator = MemberId.Random();

    private readonly DatabaseCollection<Member> _members = Database.Collection<Member>();
    private readonly LoyaltyWalletStore _walletStore = LoyaltyWalletStoreFixture.CreateStore();
    private readonly RedemptionWindowStore _windowStore = new();
    private readonly MemberTierReader _tierReader;
    private readonly EarnLoyaltyPointsHandler _earnHandler;

    public EarnLoyaltyPointsTests()
    {
        _tierReader = new MemberTierReader(_members);
        _earnHandler = new EarnLoyaltyPointsHandler(
            _windowStore.CurrentWindowsByOwners,
            _windowStore.GetRedemptionWindow,
            _windowStore.SaveRedemptionWindows,
            _tierReader.GetTier);
    }

    private async Task Enroll(MemberId memberId, Tier tier = Tier.Standard)
    {
        var @event = new MemberVerified(memberId, tier);
        await new MemberVerifiedHandler(_members).Handle(@event);
        await new OpenWalletOnMemberVerifiedHandler(_walletStore.SaveLoyaltyWallet).Handle(@event);

        var wallet = (await _walletStore.GetLoyaltyWallet(WalletNumber.ForOwner(memberId)))
            .ShouldBeOfType<LoyaltyWallet.Active>();
        await new OpenRedemptionWindowOnProgressedHandler(
                _windowStore.GetRedemptionWindow,
                _windowStore.SaveRedemptionWindow)
            .Handle(new LoyaltyWalletEvent.RedemptionWindowProgressed(
                wallet.WalletNumber,
                wallet.OwnerId,
                wallet.CurrentWindowNumber,
                LoyaltyPoints.Zero,
                wallet.MaxRedemptionCount,
                [.. wallet.Access.Members]));
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
        var current = await _windowStore.CurrentWindowOf(WalletNumber.ForOwner(memberId));
        current.ShouldNotBeNull();
        var window = (await _windowStore.GetRedemptionWindow(current.WalletNumber, current.WindowNumber))
            .ShouldBeOfType<RedemptionWindow.Open>();
        return AvailableBalance(window).Value;
    }

    private async Task<bool> HasCurrentWindow(MemberId memberId) =>
        await _windowStore.CurrentWindowOf(WalletNumber.ForOwner(memberId)) is not null;

    [Fact]
    public async Task DistributesPointsAcrossTheBuyerAndEveryBeneficiary()
    {
        await Enroll(Buyer);
        await Enroll(Referrer);
        await Enroll(Operator);

        await Earn(Purchase(Buyer));

        (await AvailableOf(Buyer)).ShouldBe(100);
        (await AvailableOf(Referrer)).ShouldBe(10);
        (await AvailableOf(Operator)).ShouldBe(5);
    }

    [Fact]
    public async Task AppliesBuyersTierToBuyersShare()
    {
        await Enroll(Buyer, Tier.Gold);
        await Enroll(Referrer);
        await Enroll(Operator);

        await Earn(Purchase(Buyer));

        (await AvailableOf(Buyer)).ShouldBe(125);
    }

    [Fact]
    public async Task SkipsABeneficiaryOutsideTheLoyaltyProgram()
    {
        await Enroll(Buyer);
        await Enroll(Referrer);

        await Earn(Purchase(Buyer));

        (await AvailableOf(Buyer)).ShouldBe(100);
        (await AvailableOf(Referrer)).ShouldBe(10);
        (await HasCurrentWindow(Operator)).ShouldBeFalse();
    }
}
