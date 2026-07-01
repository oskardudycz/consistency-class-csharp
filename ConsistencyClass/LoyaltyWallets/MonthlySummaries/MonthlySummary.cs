namespace ConsistencyClass.LoyaltyWallets.MonthlySummaries;

using ConsistencyClass.Core.Projections;
using ConsistencyClass.Membership;
using static LoyaltyWalletEvent;

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
    public static MonthlySummary Evolve(MonthlySummary? document, LoyaltyWalletEvent @event)
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
            RedemptionWindowReset => summary with
            {
                WindowsClosed = summary.WindowsClosed + 1
            },
            _ => summary
        };
    }

    public static Projection<MonthlySummary, LoyaltyWalletEvent> Projection(
        DatabaseCollection<MonthlySummary> collection) =>
        new(
            collection,
            new HashSet<Type>
            {
                typeof(LoyaltyPointsEarned),
                typeof(LoyaltyPointsRedeemed),
                typeof(RedemptionWindowReset)
            },
            DocumentId,
            Evolve);

    private static string MonthOf(DateTime at) => $"{at.Year}-{at.Month:D2}";

    private static string DocumentId(LoyaltyWalletEvent @event) =>
        $"{WalletNumberOf(@event).Value}:{MonthOf(AtOf(@event))}";

    private static WalletNumber WalletNumberOf(LoyaltyWalletEvent @event) =>
        @event switch
        {
            LoyaltyPointsEarned e => e.WalletNumber,
            LoyaltyPointsRedeemed e => e.WalletNumber,
            RedemptionWindowReset e => e.WalletNumber,
            _ => throw new ArgumentOutOfRangeException(nameof(@event))
        };

    private static MemberId OwnerOf(LoyaltyWalletEvent @event) =>
        @event switch
        {
            LoyaltyPointsEarned e => e.OwnerId,
            LoyaltyPointsRedeemed e => e.OwnerId,
            RedemptionWindowReset e => e.OwnerId,
            _ => throw new ArgumentOutOfRangeException(nameof(@event))
        };

    private static DateTime AtOf(LoyaltyWalletEvent @event) =>
        @event switch
        {
            LoyaltyPointsEarned e => e.At,
            LoyaltyPointsRedeemed e => e.At,
            RedemptionWindowReset e => e.At,
            _ => throw new ArgumentOutOfRangeException(nameof(@event))
        };
}
