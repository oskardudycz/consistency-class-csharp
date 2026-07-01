using ConsistencyClass.Core;
using ConsistencyClass.Core.Projections;
using ConsistencyClass.LoyaltyWallets.ActivityReports;
using ConsistencyClass.LoyaltyWallets.MonthlySummaries;
using ConsistencyClass.Membership;

namespace ConsistencyClass.LoyaltyWallets.RedemptionWindows;

public delegate ValueTask<RedemptionWindow> GetRedemptionWindow(WalletNumber walletNumber, int windowNumber);
public delegate ValueTask SaveRedemptionWindow(
    WalletNumber walletNumber,
    int windowNumber,
    IReadOnlyList<RedemptionWindowEvent> events,
    bool requireNew = false);

public record RedemptionWindowUpdate(
    WalletNumber WalletNumber,
    int WindowNumber,
    IReadOnlyList<RedemptionWindowEvent> Events);

public delegate ValueTask SaveRedemptionWindows(IReadOnlyList<RedemptionWindowUpdate> updates);
public delegate ValueTask<CurrentWindow?> CurrentWindowOf(WalletNumber walletNumber);
public delegate ValueTask<IReadOnlyList<CurrentWindow>> CurrentWindowsByOwners(IReadOnlyList<MemberId> ownerIds);

internal class RedemptionWindowStore
{
    private readonly EventStore<RedemptionWindowEvent> eventStore;

    public RedemptionWindowStore()
    {
        IReadOnlyList<IProjection<RedemptionWindowEvent>> projections =
        [
            ActivityReport.Projection(ActivityReports),
            MonthlySummary.Projection(MonthlySummaries),
            CurrentWindow.Projection(CurrentWindows)
        ];
        eventStore = new EventStore<RedemptionWindowEvent>(projections);
    }

    internal DatabaseCollection<ActivityReport> ActivityReports { get; } = Database.Collection<ActivityReport>();
    internal DatabaseCollection<MonthlySummary> MonthlySummaries { get; } = Database.Collection<MonthlySummary>();
    internal DatabaseCollection<CurrentWindow> CurrentWindows { get; } = Database.Collection<CurrentWindow>();

    public async ValueTask<RedemptionWindow> GetRedemptionWindow(WalletNumber walletNumber, int windowNumber)
    {
        var events = await eventStore.ReadEvents<RedemptionWindowEvent>(
            RedemptionWindowStream.Of(walletNumber, windowNumber));
        return events.Aggregate(
            (RedemptionWindow)RedemptionWindow.Initial(),
            (window, @event) => RedemptionWindowDecider.Evolve(window, @event));
    }

    public async ValueTask SaveRedemptionWindow(
        WalletNumber walletNumber,
        int windowNumber,
        IReadOnlyList<RedemptionWindowEvent> events,
        bool requireNew = false)
    {
        if (events.Count == 0)
            return;

        await eventStore.AppendToStream(RedemptionWindowStream.Of(walletNumber, windowNumber), events, requireNew);
    }

    public async ValueTask SaveRedemptionWindows(IReadOnlyList<RedemptionWindowUpdate> updates)
    {
        foreach (var update in updates)
            await SaveRedemptionWindow(update.WalletNumber, update.WindowNumber, update.Events);
    }

    public ValueTask<CurrentWindow?> CurrentWindowOf(WalletNumber walletNumber) =>
        CurrentWindows.Find(walletNumber.Value);

    public async ValueTask<IReadOnlyList<CurrentWindow>> CurrentWindowsByOwners(IReadOnlyList<MemberId> ownerIds)
    {
        var ownerSet = ownerIds.Select(id => id.Value).ToHashSet();
        var all = await CurrentWindows.GetAll();
        return all
            .Where(d => ownerSet.Contains(d.OwnerId.Value))
            .ToList();
    }
}
