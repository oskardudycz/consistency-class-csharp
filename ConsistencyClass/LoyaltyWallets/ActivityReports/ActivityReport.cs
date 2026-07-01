namespace ConsistencyClass.LoyaltyWallets.ActivityReports;

using ConsistencyClass.Core.Projections;
using ConsistencyClass.Membership;
using static LoyaltyWalletEvent;

public enum ActivityKind { Earned, Redeemed }

public record ActivityEntry(ActivityKind Kind, int Points, DateTime At);

public record WindowActivity(
    int WindowNumber,
    int Earned,
    int Redeemed,
    int Burned,
    int RedemptionCount,
    bool HadActivity,
    IReadOnlyList<ActivityEntry> Entries);

public record ActivityReport(
    WalletNumber WalletNumber,
    MemberId OwnerId,
    WindowActivity CurrentWindow,
    IReadOnlyList<WindowActivity> ClosedWindows)
{
    private static WindowActivity EmptyWindow(int windowNumber) =>
        new(windowNumber, 0, 0, 0, 0, false, []);

    public static ActivityReport? Evolve(ActivityReport? document, LoyaltyWalletEvent @event)
    {
        switch (@event)
        {
            case LoyaltyWalletOpened opened:
                return new ActivityReport(opened.WalletNumber, opened.OwnerId, EmptyWindow(1), []);
            case LoyaltyPointsEarned earned:
                {
                    if (document is null)
                        return document;

                    var window = document.CurrentWindow;
                    return document with
                    {
                        CurrentWindow = window with
                        {
                            Earned = window.Earned + earned.Points.Value,
                            HadActivity = true,
                            Entries = [.. window.Entries, new ActivityEntry(ActivityKind.Earned, earned.Points.Value, earned.At)]
                        }
                    };
                }
            case LoyaltyPointsRedeemed redeemed:
                {
                    if (document is null)
                        return document;

                    var window = document.CurrentWindow;
                    return document with
                    {
                        CurrentWindow = window with
                        {
                            Redeemed = window.Redeemed + redeemed.Points.Value,
                            Burned = window.Burned + redeemed.Burned.Value,
                            RedemptionCount = window.RedemptionCount + 1,
                            HadActivity = true,
                            Entries = [.. window.Entries, new ActivityEntry(ActivityKind.Redeemed, redeemed.Points.Value, redeemed.At)]
                        }
                    };
                }
            case RedemptionWindowReset:
                {
                    if (document is null)
                        return document;

                    return document with
                    {
                        ClosedWindows = [.. document.ClosedWindows, document.CurrentWindow],
                        CurrentWindow = EmptyWindow(document.CurrentWindow.WindowNumber + 1)
                    };
                }
            default:
                return document;
        }
    }

    public static Projection<ActivityReport, LoyaltyWalletEvent> Projection(
        DatabaseCollection<ActivityReport> collection) =>
        new(
            collection,
            new HashSet<Type>
            {
                typeof(LoyaltyWalletOpened),
                typeof(LoyaltyPointsEarned),
                typeof(LoyaltyPointsRedeemed),
                typeof(RedemptionWindowReset)
            },
            WalletNumberOf,
            Evolve);

    private static string WalletNumberOf(LoyaltyWalletEvent @event) =>
        @event switch
        {
            LoyaltyWalletOpened e => e.WalletNumber.Value,
            LoyaltyPointsEarned e => e.WalletNumber.Value,
            LoyaltyPointsRedeemed e => e.WalletNumber.Value,
            RedemptionWindowReset e => e.WalletNumber.Value,
            _ => throw new ArgumentOutOfRangeException(nameof(@event))
        };
}
