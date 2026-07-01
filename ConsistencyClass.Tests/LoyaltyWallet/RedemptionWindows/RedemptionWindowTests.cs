using ConsistencyClass.LoyaltyWallets;
using ConsistencyClass.LoyaltyWallets.Access;
using ConsistencyClass.LoyaltyWallets.RedemptionWindows;
using ConsistencyClass.Membership;
using Ogooreck.BusinessLogic;
using static ConsistencyClass.LoyaltyWallets.RedemptionWindows.RedemptionWindowCommand;
using static ConsistencyClass.LoyaltyWallets.RedemptionWindows.RedemptionWindowDecider;

namespace ConsistencyClass.Tests.LoyaltyWallets.RedemptionWindows;

public class RedemptionWindowFixture
{
    public readonly WalletNumber WalletNumber = WalletNumber.Random();
    public readonly MemberId Owner = MemberId.Random();
    public readonly MemberId FamilyMember = MemberId.Random();
    public readonly DateTime At = new(2026, 6, 23, 12, 0, 0, DateTimeKind.Utc);
    public readonly DeciderSpecification<RedemptionWindowCommand, RedemptionWindowEvent, RedemptionWindow> Spec =
        Specification.For<RedemptionWindowCommand, RedemptionWindowEvent, RedemptionWindow>(
            (command, window) => Decide(command, window).ToArray(),
            Evolve,
            RedemptionWindow.Initial);

    public static RedemptionWindow Apply(RedemptionWindow state, IReadOnlyList<RedemptionWindowEvent> events) =>
        events.Aggregate(state, Evolve);

    public RedemptionWindow.Open OpenWindow(LoyaltyPoints? openingBalance = null)
    {
        var events = OpenRedemptionWindow(
            new OpenRedemptionWindow(
                WalletNumber,
                Owner,
                1,
                openingBalance ?? LoyaltyPoints.Zero,
                RedemptionLimit.Of(5),
                [Owner]),
            RedemptionWindow.Initial());
        return (RedemptionWindow.Open)Apply(RedemptionWindow.Initial(), events);
    }

    public RedemptionWindow.Open WindowWithPoints(int points, LoyaltyPoints? openingBalance = null)
    {
        var window = OpenWindow(openingBalance);
        return (RedemptionWindow.Open)Evolve(window, EarnLoyaltyPoints(
            new EarnLoyaltyPoints(WalletNumber, LoyaltyPoints.Of(points), At),
            window));
    }
}

public class RedemptionWindowTests
{
    public class Opening(RedemptionWindowFixture fixture): IClassFixture<RedemptionWindowFixture>
    {
        [Fact]
        public void OpensNotOpenedWindow()
        {
            fixture.Spec.Given()
                .When(new OpenRedemptionWindow(
                    fixture.WalletNumber,
                    fixture.Owner,
                    1,
                    LoyaltyPoints.Zero,
                    RedemptionLimit.Of(5),
                    [fixture.Owner]))
                .Then(new RedemptionWindowEvent.RedemptionWindowOpened(
                    fixture.WalletNumber,
                    fixture.Owner,
                    1,
                    LoyaltyPoints.Zero,
                    RedemptionLimit.Of(5),
                    [fixture.Owner]));
        }
    }

    public class EarningPoints(RedemptionWindowFixture fixture): IClassFixture<RedemptionWindowFixture>
    {
        [Fact]
        public void EarnsPoints()
        {
            var window = fixture.OpenWindow();
            var earned = EarnLoyaltyPoints(
                new EarnLoyaltyPoints(fixture.WalletNumber, LoyaltyPoints.Of(100), fixture.At),
                window);
            var newState = (RedemptionWindow.Open)Evolve(window, earned);

            newState.PointsLimit.AvailablePoints.Value.ShouldBe(100);
            earned.ShouldBe(new RedemptionWindowEvent.LoyaltyPointsEarned(
                fixture.WalletNumber, fixture.Owner, 1, LoyaltyPoints.Of(100), fixture.At));
        }
    }

    public class RedeemingPoints(RedemptionWindowFixture fixture): IClassFixture<RedemptionWindowFixture>
    {
        [Fact]
        public void RedeemsPoints()
        {
            var window = fixture.WindowWithPoints(100);
            var redeemed = RedeemLoyaltyPoints(
                new RedeemLoyaltyPoints(fixture.WalletNumber, fixture.Owner, LoyaltyPoints.Of(40), fixture.At),
                window);
            var newState = (RedemptionWindow.Open)Evolve(window, redeemed);

            newState.PointsLimit.AvailablePoints.Value.ShouldBe(60);
            redeemed.ShouldBe(new RedemptionWindowEvent.LoyaltyPointsRedeemed(
                fixture.WalletNumber,
                fixture.Owner,
                1,
                fixture.Owner,
                LoyaltyPoints.Of(40),
                LoyaltyPoints.Of(40),
                fixture.At));
        }
    }

    public class Balance(RedemptionWindowFixture fixture): IClassFixture<RedemptionWindowFixture>
    {
        [Fact]
        public void UsesOpeningBalanceAsAvailableBalance()
        {
            var window = fixture.OpenWindow(LoyaltyPoints.Of(60));

            AvailableBalance(window).ShouldBe(LoyaltyPoints.Of(60));
        }
    }

    public class Access(RedemptionWindowFixture fixture): IClassFixture<RedemptionWindowFixture>
    {
        [Fact]
        public void GrantsAndRevokesWindowAccess()
        {
            var window = fixture.OpenWindow();
            var granted = GrantWindowAccess(new GrantWindowAccess(fixture.WalletNumber, fixture.FamilyMember), window);
            var withAccess = (RedemptionWindow.Open)RedemptionWindowFixture.Apply(window, granted);
            var revoked = RevokeWindowAccess(new RevokeWindowAccess(fixture.WalletNumber, fixture.FamilyMember), withAccess);
            var withoutAccess = (RedemptionWindow.Open)RedemptionWindowFixture.Apply(withAccess, revoked);

            withAccess.Access.ShouldBe(WalletAccess.Of(fixture.Owner, fixture.FamilyMember));
            withoutAccess.Access.ShouldBe(WalletAccess.Of(fixture.Owner));
        }
    }

    public class Lifecycle(RedemptionWindowFixture fixture): IClassFixture<RedemptionWindowFixture>
    {
        [Fact]
        public void ClosesWindow()
        {
            var window = fixture.WindowWithPoints(100);
            var closed = CloseRedemptionWindow(
                new CloseRedemptionWindow(fixture.WalletNumber, fixture.At),
                window);
            var newState = (RedemptionWindow.Closed)RedemptionWindowFixture.Apply(window, closed);

            newState.ClosingBalance.Value.ShouldBe(100);
            closed.ShouldBe([
                new RedemptionWindowEvent.RedemptionWindowClosed(
                    fixture.WalletNumber,
                    fixture.Owner,
                    1,
                    LoyaltyPoints.Of(100),
                    RedemptionLimit.Zero,
                    true,
                    fixture.At)
            ]);
        }
    }
}
