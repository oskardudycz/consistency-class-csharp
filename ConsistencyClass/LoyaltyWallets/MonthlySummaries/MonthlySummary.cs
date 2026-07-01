namespace ConsistencyClass.LoyaltyWallets.MonthlySummaries;

using ConsistencyClass.Core;
using ConsistencyClass.Core.Projections;
using ConsistencyClass.LoyaltyWallets.RedemptionWindows;
using ConsistencyClass.Membership;
using static ConsistencyClass.LoyaltyWallets.RedemptionWindows.RedemptionWindowEvent;

public record MonthlySummary(
    string Id,
    WalletNumber WalletNumber,
    MemberId OwnerId,
    string Month,
    int TotalEarned,
    int TotalRedeemed,
    int TotalBurned,
    int RedemptionCount,
    int WindowsClosed)
{
    public static MonthlySummary Evolve(MonthlySummary? document, RedemptionWindowEvent @event)
    {
        var summary = document ?? new MonthlySummary(
            DocumentId(@event),
            WalletNumberOf(@event),
            OwnerOf(@event),
            MonthOf(AtOf(@event)),
            0, 0, 0, 0, 0);

        return @event switch
        {
            LoyaltyPointsEarned earned => summary with
            {
                TotalEarned = summary.TotalEarned + earned.Points.Value
            },
            LoyaltyPointsRedeemed redeemed => summary with
            {
                TotalRedeemed = summary.TotalRedeemed + redeemed.Points.Value,
                TotalBurned = summary.TotalBurned + redeemed.Burned.Value,
                RedemptionCount = summary.RedemptionCount + 1
            },
            RedemptionWindowClosed => summary with
            {
                WindowsClosed = summary.WindowsClosed + 1
            },
            _ => summary
        };
    }

    public static Projection<MonthlySummary, RedemptionWindowEvent> Projection(
        DatabaseCollection<MonthlySummary> collection) =>
        new(
            collection,
            new HashSet<Type>
            {
                typeof(LoyaltyPointsEarned),
                typeof(LoyaltyPointsRedeemed),
                typeof(RedemptionWindowClosed)
            },
            DocumentId,
            Evolve);

    private static string MonthOf(DateTime at) => $"{at.Year}-{at.Month:D2}";

    private static string DocumentId(RedemptionWindowEvent @event) =>
        $"{WalletNumberOf(@event).Value}:{MonthOf(AtOf(@event))}";

    private static WalletNumber WalletNumberOf(RedemptionWindowEvent @event) =>
        @event switch
        {
            LoyaltyPointsEarned e => e.WalletNumber,
            LoyaltyPointsRedeemed e => e.WalletNumber,
            RedemptionWindowClosed e => e.WalletNumber,
            _ => throw new ArgumentOutOfRangeException(nameof(@event))
        };

    private static MemberId OwnerOf(RedemptionWindowEvent @event) =>
        @event switch
        {
            LoyaltyPointsEarned e => e.OwnerId,
            LoyaltyPointsRedeemed e => e.OwnerId,
            RedemptionWindowClosed e => e.OwnerId,
            _ => throw new ArgumentOutOfRangeException(nameof(@event))
        };

    private static DateTime AtOf(RedemptionWindowEvent @event) =>
        @event switch
        {
            LoyaltyPointsEarned e => e.At,
            LoyaltyPointsRedeemed e => e.At,
            RedemptionWindowClosed e => e.ClosedAt,
            _ => throw new ArgumentOutOfRangeException(nameof(@event))
        };
}
