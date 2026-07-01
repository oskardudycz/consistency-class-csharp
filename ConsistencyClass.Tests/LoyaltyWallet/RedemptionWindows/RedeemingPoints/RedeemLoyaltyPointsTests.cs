using ConsistencyClass.LoyaltyWallets;
using ConsistencyClass.LoyaltyWallets.Access;
using ConsistencyClass.LoyaltyWallets.RedemptionWindows;
using ConsistencyClass.LoyaltyWallets.RedemptionWindows.Access;
using ConsistencyClass.LoyaltyWallets.RedemptionWindows.RedeemingPoints;
using ConsistencyClass.LoyaltyWallets.RedemptionWindows.WindowLifecycle;
using ConsistencyClass.LoyaltyWallets.WalletLifecycle;
using ConsistencyClass.Membership;
using ConsistencyClass.Membership.MemberDirectory;
using static ConsistencyClass.LoyaltyWallets.LoyaltyWalletCommand;
using static ConsistencyClass.LoyaltyWallets.RedemptionWindows.RedemptionWindowCommand;
using static ConsistencyClass.LoyaltyWallets.RedemptionWindows.RedemptionWindowDecider;

namespace ConsistencyClass.Tests.LoyaltyWallets.RedemptionWindows.RedeemingPoints;

public class RedeemLoyaltyPointsTests
{
    private static readonly MemberId Oskar = MemberId.Random();
    private static readonly MemberId Kuba = MemberId.Random();
    private static readonly DateTime At = new(2026, 6, 23, 12, 0, 0, DateTimeKind.Utc);

    private readonly DatabaseCollection<Member> _members = Database.Collection<Member>();
    private readonly LoyaltyWalletStore _walletStore = LoyaltyWalletStoreFixture.CreateStore();
    private readonly RedemptionWindowStore _windowStore = new();
    private readonly MemberTierReader _tierReader;
    private readonly RedeemLoyaltyPointsHandler _redeemHandler;

    public RedeemLoyaltyPointsTests()
    {
        _tierReader = new MemberTierReader(_members);
        _redeemHandler = new RedeemLoyaltyPointsHandler(
            _windowStore.CurrentWindowOf,
            _windowStore.GetRedemptionWindow,
            _windowStore.SaveRedemptionWindow,
            _tierReader.GetTier);
    }

    private async Task Enroll(MemberId memberId, Tier tier = Tier.Standard)
    {
        var @event = new MemberVerified(memberId, tier);
        await new MemberVerifiedHandler(_members).Handle(@event);
        await new OpenWalletOnMemberVerifiedHandler(_walletStore.SaveLoyaltyWallet).Handle(@event);
        await OpenCurrentWindow(memberId);
    }

    private async Task OpenCurrentWindow(MemberId memberId)
    {
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

    private static WalletNumber WalletNumberOf(MemberId memberId) => WalletNumber.ForOwner(memberId);

    private async Task Earn(WalletNumber walletNumber, int points)
    {
        var current = await _windowStore.CurrentWindowOf(walletNumber);
        current.ShouldNotBeNull();
        var window = await _windowStore.GetRedemptionWindow(walletNumber, current.WindowNumber);
        var earned = EarnLoyaltyPoints(
            new EarnLoyaltyPoints(walletNumber, LoyaltyPoints.Of(points), At), window);
        await _windowStore.SaveRedemptionWindow(walletNumber, current.WindowNumber, [earned]);
    }

    private Task Redeem(WalletNumber walletNumber, MemberId memberId, int points) =>
        _redeemHandler.Handle(new RedeemLoyaltyPoints(walletNumber, memberId, LoyaltyPoints.Of(points), At)).AsTask();

    private async Task GrantAccess(WalletNumber walletNumber, MemberId memberId)
    {
        await new GrantWalletAccessHandler(_walletStore.GetLoyaltyWallet, _walletStore.SaveLoyaltyWallet)
            .Handle(new GrantWalletAccess(walletNumber, memberId));
        await new PropagateAccessToWindowHandler(
                _windowStore.CurrentWindowOf,
                _windowStore.GetRedemptionWindow,
                _windowStore.SaveRedemptionWindow)
            .Handle(new LoyaltyWalletEvent.WalletAccessGranted(walletNumber, memberId));
    }

    private async Task RevokeAccess(WalletNumber walletNumber, MemberId memberId)
    {
        await new RevokeWalletAccessHandler(_walletStore.GetLoyaltyWallet, _walletStore.SaveLoyaltyWallet)
            .Handle(new RevokeWalletAccess(walletNumber, memberId));
        await new PropagateAccessToWindowHandler(
                _windowStore.CurrentWindowOf,
                _windowStore.GetRedemptionWindow,
                _windowStore.SaveRedemptionWindow)
            .Handle(new LoyaltyWalletEvent.WalletAccessRevoked(walletNumber, memberId));
    }

    private async Task<RedemptionWindow.Open> ActiveWindow(WalletNumber walletNumber)
    {
        var current = await _windowStore.CurrentWindowOf(walletNumber);
        current.ShouldNotBeNull();
        return (await _windowStore.GetRedemptionWindow(walletNumber, current.WindowNumber))
            .ShouldBeOfType<RedemptionWindow.Open>();
    }

    private async Task CloseAndProgress(WalletNumber walletNumber)
    {
        var current = await _windowStore.CurrentWindowOf(walletNumber);
        current.ShouldNotBeNull();
        await new CloseRedemptionWindowHandler(
                _windowStore.CurrentWindowOf,
                _windowStore.GetRedemptionWindow,
                _windowStore.SaveRedemptionWindow)
            .Handle(new CloseRedemptionWindow(walletNumber, At));
        var closed = (await _windowStore.GetRedemptionWindow(walletNumber, current.WindowNumber))
            .ShouldBeOfType<RedemptionWindow.Closed>();
        await new ProgressWalletOnRedemptionWindowClosedHandler(
                _walletStore.GetLoyaltyWallet,
                _walletStore.SaveLoyaltyWallet)
            .Handle(new RedemptionWindowEvent.RedemptionWindowClosed(
                closed.WalletNumber,
                closed.OwnerId,
                closed.WindowNumber,
                closed.ClosingBalance,
                closed.RedemptionCount,
                true,
                At));
        var wallet = (await _walletStore.GetLoyaltyWallet(walletNumber)).ShouldBeOfType<LoyaltyWallet.Active>();
        await new OpenRedemptionWindowOnProgressedHandler(
                _windowStore.GetRedemptionWindow,
                _windowStore.SaveRedemptionWindow)
            .Handle(new LoyaltyWalletEvent.RedemptionWindowProgressed(
                wallet.WalletNumber,
                wallet.OwnerId,
                wallet.CurrentWindowNumber,
                closed.ClosingBalance,
                wallet.MaxRedemptionCount,
                [.. wallet.Access.Members]));
    }

    [Fact]
    public async Task RedeemsPointsFromAnOpenWindow()
    {
        await Enroll(Oskar);
        var walletNumber = WalletNumberOf(Oskar);
        await Earn(walletNumber, 100);

        await Redeem(walletNumber, Oskar, 40);

        var window = await ActiveWindow(walletNumber);
        AvailableBalance(window).Value.ShouldBe(60);
        RedemptionsLeft(window).Value.ShouldBe(2);
    }

    [Fact]
    public async Task HigherOwnerTierBurnsFewerPointsThanRedeemed()
    {
        await Enroll(Oskar, Tier.Gold);
        var walletNumber = WalletNumberOf(Oskar);
        await Earn(walletNumber, 100);

        await Redeem(walletNumber, Oskar, 100);

        AvailableBalance(await ActiveWindow(walletNumber)).Value.ShouldBe(5);
    }

    [Fact]
    public async Task AGrantedFamilyMemberRedeemsOnTheOwnersTier()
    {
        await Enroll(Oskar, Tier.Gold);
        await Enroll(Kuba);
        var walletNumber = WalletNumberOf(Oskar);
        await Earn(walletNumber, 100);
        await GrantAccess(walletNumber, Kuba);

        await Redeem(walletNumber, Kuba, 100);

        AvailableBalance(await ActiveWindow(walletNumber)).Value.ShouldBe(5);
    }

    [Fact]
    public async Task CantRedeemAfterAccessIsRevoked()
    {
        await Enroll(Oskar);
        await Enroll(Kuba);
        var walletNumber = WalletNumberOf(Oskar);
        await Earn(walletNumber, 100);
        await GrantAccess(walletNumber, Kuba);
        await RevokeAccess(walletNumber, Kuba);

        await Should.ThrowAsync<InvalidOperationException>(() => Redeem(walletNumber, Kuba, 40));
    }

    [Fact]
    public async Task CanRedeemAgainAfterTheWindowIsClosedAndNextOpened()
    {
        await Enroll(Oskar);
        var walletNumber = WalletNumberOf(Oskar);
        await Earn(walletNumber, 100);
        await Redeem(walletNumber, Oskar, 10);
        await Redeem(walletNumber, Oskar, 10);
        await Redeem(walletNumber, Oskar, 10);

        await CloseAndProgress(walletNumber);
        await Redeem(walletNumber, Oskar, 10);

        AvailableBalance(await ActiveWindow(walletNumber)).Value.ShouldBe(60);
    }
}
