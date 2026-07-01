using ConsistencyClass.LoyaltyWallets;
using ConsistencyClass.LoyaltyWallets.MonthlySummaries;
using ConsistencyClass.LoyaltyWallets.RedemptionWindows;
using ConsistencyClass.Membership;
using static ConsistencyClass.LoyaltyWallets.RedemptionWindows.RedemptionWindowEvent;

namespace ConsistencyClass.Tests.LoyaltyWallets.MonthlySummaries;

public class MonthlySummaryTests
{
    private readonly WalletNumber _walletNumber = WalletNumber.Random();
    private readonly MemberId _owner = MemberId.Random();
    private readonly DateTime _june = new(2026, 6, 23, 12, 0, 0, DateTimeKind.Utc);

    private RedemptionWindowEvent Earned(int points, DateTime at) =>
        new LoyaltyPointsEarned(_walletNumber, _owner, 1, LoyaltyPoints.Of(points), at);

    private RedemptionWindowEvent Redeemed(int points, DateTime at, int burned) =>
        new LoyaltyPointsRedeemed(_walletNumber, _owner, 1, _owner, LoyaltyPoints.Of(points), LoyaltyPoints.Of(burned), at);

    private RedemptionWindowEvent Closed(DateTime at) =>
        new RedemptionWindowClosed(_walletNumber, _owner, 1, LoyaltyPoints.Zero, RedemptionLimit.Zero, true, at);

    private static MonthlySummary Fold(params RedemptionWindowEvent[] events) =>
        events.Aggregate((MonthlySummary?)null, MonthlySummary.Evolve)!;

    [Fact]
    public void AggregatesEarnsRedemptionsAndClosedWindowsForTheMonth()
    {
        var summary = Fold(Earned(100, _june), Redeemed(40, _june, 38), Redeemed(10, _june, 10), Closed(_june));

        summary.Month.ShouldBe("2026-06");
        summary.Id.ShouldBe($"{_walletNumber.Value}:2026-06");
        summary.OwnerId.ShouldBe(_owner);
        summary.TotalEarned.ShouldBe(100);
        summary.TotalRedeemed.ShouldBe(50);
        summary.TotalBurned.ShouldBe(48);
        summary.RedemptionCount.ShouldBe(2);
        summary.WindowsClosed.ShouldBe(1);
    }

    [Fact]
    public void InitialisesTheDocumentOnARedemptionWithNoPriorEarn()
    {
        var summary = Fold(Redeemed(25, _june, 25));

        summary.OwnerId.ShouldBe(_owner);
        summary.TotalEarned.ShouldBe(0);
        summary.TotalRedeemed.ShouldBe(25);
        summary.RedemptionCount.ShouldBe(1);
    }
}
