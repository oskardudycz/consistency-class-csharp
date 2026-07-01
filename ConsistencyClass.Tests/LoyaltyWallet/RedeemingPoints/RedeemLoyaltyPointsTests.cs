using ConsistencyClass.LoyaltyWallets;
using ConsistencyClass.LoyaltyWallets.Access;
using ConsistencyClass.LoyaltyWallets.RedeemingPoints;
using ConsistencyClass.LoyaltyWallets.WalletLifecycle;
using ConsistencyClass.Membership;
using ConsistencyClass.Membership.MemberDirectory;
using static ConsistencyClass.LoyaltyWallets.LoyaltyWalletCommand;

namespace ConsistencyClass.Tests.LoyaltyWallets.RedeemingPoints;

public class RedeemLoyaltyPointsTests
{
    private static readonly MemberId Oskar = MemberId.Random();
    private static readonly MemberId Kuba = MemberId.Random();
    private static readonly DateTime At = new(2026, 6, 23, 12, 0, 0, DateTimeKind.Utc);

    private readonly DatabaseCollection<Member> _members = Database.Collection<Member>();
    private readonly LoyaltyWalletStore _store = LoyaltyWalletStoreFixture.CreateStore();
    private readonly MemberTierReader _tierReader;
    private readonly RedeemLoyaltyPointsHandler _redeemHandler;

    public RedeemLoyaltyPointsTests()
    {
        _tierReader = new MemberTierReader(_members);
        _redeemHandler = new RedeemLoyaltyPointsHandler(
            _store.GetLoyaltyWallet,
            _store.SaveLoyaltyWallet,
            _tierReader.GetTier);
    }

    private async Task Enroll(MemberId memberId, Tier tier = Tier.Standard)
    {
        var @event = new MemberVerified(memberId, tier);
        await new MemberVerifiedHandler(_members).Handle(@event);
        await new OpenWalletOnMemberVerifiedHandler(_store.SaveLoyaltyWallet).Handle(@event);
    }

    private async Task<WalletNumber> WalletNumberOf(MemberId memberId)
    {
        var wallets = await _store.FindLoyaltyWalletsByOwners([memberId]);
        return wallets.ShouldHaveSingleItem().ShouldBeOfType<LoyaltyWallet.Active>().WalletNumber;
    }

    private async Task Earn(WalletNumber walletNumber, int points)
    {
        var wallet = await _store.GetLoyaltyWallet(walletNumber);
        var earned = LoyaltyWalletDecider.EarnLoyaltyPoints(
            new EarnLoyaltyPoints(walletNumber, LoyaltyPoints.Of(points), At), wallet);
        await _store.SaveLoyaltyWallet(walletNumber, [earned]);
    }

    private Task Redeem(WalletNumber walletNumber, MemberId memberId, int points) =>
        _redeemHandler.Handle(new RedeemLoyaltyPoints(walletNumber, memberId, LoyaltyPoints.Of(points), At)).AsTask();

    private Task GrantAccess(WalletNumber walletNumber, MemberId memberId) =>
        new GrantWalletAccessHandler(_store.GetLoyaltyWallet, _store.SaveLoyaltyWallet)
            .Handle(new GrantWalletAccess(walletNumber, memberId)).AsTask();

    private Task RevokeAccess(WalletNumber walletNumber, MemberId memberId) =>
        new RevokeWalletAccessHandler(_store.GetLoyaltyWallet, _store.SaveLoyaltyWallet)
            .Handle(new RevokeWalletAccess(walletNumber, memberId)).AsTask();

    private Task ResetWindow(WalletNumber walletNumber) =>
        new ResetRedemptionWindowHandler(_store.GetLoyaltyWallet, _store.SaveLoyaltyWallet)
            .Handle(new ResetRedemptionWindow(walletNumber, At)).AsTask();

    private async Task<LoyaltyWallet.Active> ActiveWallet(WalletNumber walletNumber)
    {
        var wallet = await _store.GetLoyaltyWallet(walletNumber);
        return wallet.ShouldBeOfType<LoyaltyWallet.Active>();
    }

    [Fact]
    public async Task RedeemsPointsFromAnActiveWallet()
    {
        // given
        await Enroll(Oskar);
        var walletNumber = await WalletNumberOf(Oskar);
        await Earn(walletNumber, 100);

        // when
        await Redeem(walletNumber, Oskar, 40);

        // then
        var wallet = await ActiveWallet(walletNumber);
        wallet.PointsLimit.AvailablePoints.Value.ShouldBe(60);
        wallet.PointsLimit.RedemptionsLeft.ShouldBe(2);
    }

    [Fact]
    public async Task HigherOwnerTierBurnsFeverPointsThanRedeemed()
    {
        // given a Gold owner
        await Enroll(Oskar, Tier.Gold);
        var walletNumber = await WalletNumberOf(Oskar);
        await Earn(walletNumber, 100);

        // when
        await Redeem(walletNumber, Oskar, 100);

        // then
        (await ActiveWallet(walletNumber)).PointsLimit.AvailablePoints.Value.ShouldBe(5);
    }

    [Fact]
    public async Task CantRedeemMoreThanAvailable()
    {
        // given
        await Enroll(Oskar);
        var walletNumber = await WalletNumberOf(Oskar);
        await Earn(walletNumber, 20);

        // when
        await Should.ThrowAsync<InvalidOperationException>(() => Redeem(walletNumber, Oskar, 50));

        // then
        (await ActiveWallet(walletNumber)).PointsLimit.AvailablePoints.Value.ShouldBe(20);
    }

    [Fact]
    public async Task AGrantedFamilyMemberRedeemsOnTheOwnersTier()
    {
        // given a Gold owner and a Standard family member
        await Enroll(Oskar, Tier.Gold);
        await Enroll(Kuba);
        var walletNumber = await WalletNumberOf(Oskar);
        await Earn(walletNumber, 100);
        await GrantAccess(walletNumber, Kuba);

        // when
        await Redeem(walletNumber, Kuba, 100);

        // then the owner's Gold tier drives the burn, not the redeemer's Standard
        (await ActiveWallet(walletNumber)).PointsLimit.AvailablePoints.Value.ShouldBe(5);
    }

    [Fact]
    public async Task CantRedeemWithoutAccess()
    {
        // given
        await Enroll(Oskar);
        await Enroll(Kuba);
        var walletNumber = await WalletNumberOf(Oskar);
        await Earn(walletNumber, 100);

        // when
        await Should.ThrowAsync<InvalidOperationException>(() => Redeem(walletNumber, Kuba, 40));

        // then
        (await ActiveWallet(walletNumber)).PointsLimit.AvailablePoints.Value.ShouldBe(100);
    }

    [Fact]
    public async Task CantRedeemAfterAccessIsRevoked()
    {
        // given
        await Enroll(Oskar);
        await Enroll(Kuba);
        var walletNumber = await WalletNumberOf(Oskar);
        await Earn(walletNumber, 100);
        await GrantAccess(walletNumber, Kuba);
        await Redeem(walletNumber, Kuba, 40);
        await RevokeAccess(walletNumber, Kuba);

        // when
        await Should.ThrowAsync<InvalidOperationException>(() => Redeem(walletNumber, Kuba, 40));

        // then
        (await ActiveWallet(walletNumber)).PointsLimit.AvailablePoints.Value.ShouldBe(60);
    }

    [Fact]
    public async Task CantRedeemMoreTimesThanTheWindowAllows()
    {
        // given a Standard wallet, the window allows three redemptions
        await Enroll(Oskar);
        var walletNumber = await WalletNumberOf(Oskar);
        await Earn(walletNumber, 100);
        // and the window is used up
        await Redeem(walletNumber, Oskar, 10);
        await Redeem(walletNumber, Oskar, 10);
        await Redeem(walletNumber, Oskar, 10);

        // when
        await Should.ThrowAsync<InvalidOperationException>(() => Redeem(walletNumber, Oskar, 10));

        // then
        (await ActiveWallet(walletNumber)).PointsLimit.AvailablePoints.Value.ShouldBe(70);
    }

    [Fact]
    public async Task CanRedeemAgainAfterTheWindowIsReset()
    {
        // given a Standard wallet
        await Enroll(Oskar);
        var walletNumber = await WalletNumberOf(Oskar);
        await Earn(walletNumber, 100);
        // and the window is used up
        await Redeem(walletNumber, Oskar, 10);
        await Redeem(walletNumber, Oskar, 10);
        await Redeem(walletNumber, Oskar, 10);

        // when
        await ResetWindow(walletNumber);
        await Redeem(walletNumber, Oskar, 10);

        // then
        (await ActiveWallet(walletNumber)).PointsLimit.AvailablePoints.Value.ShouldBe(60);
    }

    [Fact]
    public async Task CantRedeemWhenTheOwnerIsMissingFromTheDirectory()
    {
        // given an active wallet with no member record behind it
        var walletNumber = WalletNumber.Random();
        await new OpenLoyaltyWalletHandler(_store.GetLoyaltyWallet, _store.SaveLoyaltyWallet)
            .Handle(new OpenLoyaltyWallet(walletNumber, Oskar, RedemptionLimit.Of(5), RedemptionCadence.Weekly));
        await Earn(walletNumber, 100);

        // when
        await Should.ThrowAsync<InvalidOperationException>(() => Redeem(walletNumber, Oskar, 40));

        // then
        (await ActiveWallet(walletNumber)).PointsLimit.AvailablePoints.Value.ShouldBe(100);
    }
}
