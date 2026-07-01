using ConsistencyClass.LoyaltyWallets;
using ConsistencyClass.LoyaltyWallets.ActivityReports;
using ConsistencyClass.LoyaltyWallets.RedemptionWindows;
using ConsistencyClass.Membership;
using static ConsistencyClass.LoyaltyWallets.RedemptionWindows.RedemptionWindowEvent;

namespace ConsistencyClass.Tests.LoyaltyWallets.ActivityReports;

public class ActivityReportTests
{
    private readonly WalletNumber _walletNumber = WalletNumber.Random();
    private readonly MemberId _owner = MemberId.Random();
    private readonly DateTime _at = new(2026, 6, 23, 12, 0, 0, DateTimeKind.Utc);

    private RedemptionWindowEvent Opened(int windowNumber = 1) =>
        new RedemptionWindowOpened(_walletNumber, _owner, windowNumber, LoyaltyPoints.Zero, RedemptionLimit.Of(5), [_owner]);

    private RedemptionWindowEvent Earned(int points, int windowNumber = 1) =>
        new LoyaltyPointsEarned(_walletNumber, _owner, windowNumber, LoyaltyPoints.Of(points), _at);

    private RedemptionWindowEvent Redeemed(int points, int burned, int windowNumber = 1) =>
        new LoyaltyPointsRedeemed(_walletNumber, _owner, windowNumber, _owner, LoyaltyPoints.Of(points), LoyaltyPoints.Of(burned), _at);

    private RedemptionWindowEvent Closed(int windowNumber = 1) =>
        new RedemptionWindowClosed(_walletNumber, _owner, windowNumber, LoyaltyPoints.Zero, RedemptionLimit.Zero, true, _at);

    private static ActivityReport? Fold(params RedemptionWindowEvent[] events) =>
        events.Aggregate((ActivityReport?)null, ActivityReport.Evolve);

    [Fact]
    public void OpensAnEmptyReportInItsFirstWindow()
    {
        var report = Fold(Opened());

        report.ShouldNotBeNull();
        report.WalletNumber.ShouldBe(_walletNumber);
        report.OwnerId.ShouldBe(_owner);
        report.CurrentWindow.WindowNumber.ShouldBe(1);
        report.CurrentWindow.HadActivity.ShouldBeFalse();
        report.ClosedWindows.ShouldBeEmpty();
    }

    [Fact]
    public void GroupsEarnsAndRedemptionsIntoTheCurrentWindow()
    {
        var report = Fold(Opened(), Earned(100), Redeemed(40, 38));

        report.ShouldNotBeNull();
        report.CurrentWindow.Earned.ShouldBe(100);
        report.CurrentWindow.Redeemed.ShouldBe(40);
        report.CurrentWindow.Burned.ShouldBe(38);
        report.CurrentWindow.RedemptionCount.ShouldBe(1);
        report.CurrentWindow.HadActivity.ShouldBeTrue();
        report.CurrentWindow.Entries.ShouldBe(new[]
        {
            new ActivityEntry(ActivityKind.Earned, 100, _at),
            new ActivityEntry(ActivityKind.Redeemed, 40, _at)
        });
    }

    [Fact]
    public void ClosesTheCurrentWindowAndOpensTheNext()
    {
        var report = Fold(Opened(), Earned(100), Redeemed(40, 38), Closed(), Opened(2), Earned(20, 2));

        report.ShouldNotBeNull();
        report.ClosedWindows.Count.ShouldBe(1);
        report.ClosedWindows[0].WindowNumber.ShouldBe(1);
        report.ClosedWindows[0].Earned.ShouldBe(100);
        report.ClosedWindows[0].Redeemed.ShouldBe(40);
        report.ClosedWindows[0].Burned.ShouldBe(38);
        report.CurrentWindow.WindowNumber.ShouldBe(2);
        report.CurrentWindow.Earned.ShouldBe(20);
        report.CurrentWindow.Redeemed.ShouldBe(0);
    }

    [Fact]
    public void IgnoresActivityBeforeTheReportIsOpened()
    {
        var report = Fold(Earned(100));

        report.ShouldBeNull();
    }
}
