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
    private readonly IReadOnlyList<IProjection<LoyaltyWalletEvent>> projections;

    public LoyaltyWalletStore(DatabaseCollection<WalletDetailsDocument> wallets)
    {
        Wallets = wallets;
        projections =
        [
            ActivityReport.Projection(ActivityReports),
            MonthlySummary.Projection(MonthlySummaries),
            WalletDetailsDocument.Projection(Wallets)
        ];
    }

    internal DatabaseCollection<WalletDetailsDocument> Wallets { get; }
    internal DatabaseCollection<ActivityReport> ActivityReports { get; } = Database.Collection<ActivityReport>();
    internal DatabaseCollection<MonthlySummary> MonthlySummaries { get; } = Database.Collection<MonthlySummary>();

    public async ValueTask<LoyaltyWallet> GetLoyaltyWallet(WalletNumber walletNumber)
    {
        var doc = await Wallets.Find(walletNumber.Value);
        return doc is not null ? WalletDetailsDocument.ToWallet(doc) : LoyaltyWallet.Initial();
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
        await Projections.ApplyProjections(projections, events);
    }

    public async ValueTask SaveLoyaltyWallets(IReadOnlyList<LoyaltyWalletUpdate> updates)
    {
        foreach (var update in updates)
            await SaveLoyaltyWallet(update.WalletNumber, update.Events);
    }
}
