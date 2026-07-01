namespace ConsistencyClass.LoyaltyWallets;

using ConsistencyClass.Core.Projections;
using ConsistencyClass.LoyaltyWallets.ActivityReports;
using ConsistencyClass.LoyaltyWallets.MonthlySummaries;
using ConsistencyClass.Membership;
using WalletDetailsDocument = ConsistencyClass.LoyaltyWallets.WalletDetails.WalletDetails;

public delegate ValueTask<LoyaltyWallet> GetLoyaltyWallet(WalletNumber walletNumber);
public delegate ValueTask<IReadOnlyList<LoyaltyWallet>> FindLoyaltyWalletsByOwners(IReadOnlyList<MemberId> ownerIds);
public delegate ValueTask SaveLoyaltyWallet(WalletNumber walletNumber, IReadOnlyList<LoyaltyWalletEvent> events);

public record LoyaltyWalletUpdate(WalletNumber WalletNumber, IReadOnlyList<LoyaltyWalletEvent> Events);
public delegate ValueTask SaveLoyaltyWallets(IReadOnlyList<LoyaltyWalletUpdate> updates);

internal class LoyaltyWalletStore
{
    private readonly EventStore<LoyaltyWalletEvent> eventStore;

    public LoyaltyWalletStore(DatabaseCollection<WalletDetailsDocument> wallets)
    {
        Wallets = wallets;
        IReadOnlyList<IProjection<LoyaltyWalletEvent>> projections =
        [
            ActivityReport.Projection(ActivityReports),
            MonthlySummary.Projection(MonthlySummaries),
            WalletDetailsDocument.Projection(Wallets)
        ];
        eventStore = new EventStore<LoyaltyWalletEvent>(projections);
    }

    internal DatabaseCollection<WalletDetailsDocument> Wallets { get; }
    internal DatabaseCollection<ActivityReport> ActivityReports { get; } = Database.Collection<ActivityReport>();
    internal DatabaseCollection<MonthlySummary> MonthlySummaries { get; } = Database.Collection<MonthlySummary>();

    public async ValueTask<LoyaltyWallet> GetLoyaltyWallet(WalletNumber walletNumber)
    {
        var events = await eventStore.ReadEvents<LoyaltyWalletEvent>(walletNumber.Value);
        return events.Aggregate(
            (LoyaltyWallet)LoyaltyWallet.Initial(),
            (wallet, @event) => LoyaltyWalletDecider.Evolve(wallet, @event));
    }

    public async ValueTask<IReadOnlyList<LoyaltyWallet>> FindLoyaltyWalletsByOwners(IReadOnlyList<MemberId> ownerIds)
    {
        var ownerSet = ownerIds.Select(id => id.Value).ToHashSet();
        var all = await Wallets.GetAll();
        return all
            .Where(d => ownerSet.Contains(d.OwnerId.Value))
            .Select(WalletDetailsDocument.ToWallet)
            .ToList();
    }

    public async ValueTask SaveLoyaltyWallet(WalletNumber walletNumber, IReadOnlyList<LoyaltyWalletEvent> events)
    {
        if (events.Count == 0)
            return;

        await eventStore.AppendToStream(walletNumber.Value, events);
    }

    public async ValueTask SaveLoyaltyWallets(IReadOnlyList<LoyaltyWalletUpdate> updates)
    {
        foreach (var update in updates)
            await SaveLoyaltyWallet(update.WalletNumber, update.Events);
    }
}
