namespace ConsistencyClass.LoyaltyWallets;

using ConsistencyClass.LoyaltyWallets.Access;
using ConsistencyClass.Membership;

public delegate ValueTask<LoyaltyWallet> GetLoyaltyWallet(WalletNumber walletNumber);
public delegate ValueTask<IReadOnlyList<LoyaltyWallet>> FindLoyaltyWalletsByOwners(IReadOnlyList<MemberId> ownerIds);
public delegate ValueTask SaveLoyaltyWallet(LoyaltyWallet wallet);
public delegate ValueTask SaveLoyaltyWallets(IReadOnlyList<LoyaltyWallet> wallets);

internal abstract record WalletDocument
{
    private WalletDocument() { }

    internal sealed record ActiveOrDeactivated(
        WalletNumber WalletNumber,
        string Status,
        MemberId OwnerId,
        RedemptionCadence Cadence,
        int EarnedPoints,
        int RedeemedPoints,
        int RedemptionCount,
        int MaxRedemptionCount,
        IReadOnlyList<MemberId> AccessMembers): WalletDocument;

    internal sealed record Closed(WalletNumber WalletNumber): WalletDocument;
}

internal class LoyaltyWalletStore(DatabaseCollection<WalletDocument> wallets)
{
    public async ValueTask<LoyaltyWallet> GetLoyaltyWallet(WalletNumber walletNumber)
    {
        var doc = await wallets.Find(walletNumber.Value);
        return doc is not null ? FromDocument(doc) : LoyaltyWallet.Initial();
    }

    public async ValueTask<IReadOnlyList<LoyaltyWallet>> FindLoyaltyWalletsByOwners(IReadOnlyList<MemberId> ownerIds)
    {
        var ownerSet = ownerIds.Select(id => id.Value).ToHashSet();
        var all = await wallets.GetAll();
        return all
            .OfType<WalletDocument.ActiveOrDeactivated>()
            .Where(d => ownerSet.Contains(d.OwnerId.Value))
            .Select(d => (LoyaltyWallet)FromDocument(d))
            .ToList();
    }

    public async ValueTask SaveLoyaltyWallet(LoyaltyWallet wallet)
    {
        var (key, doc) = ToDocument(wallet);
        await wallets.Save(key, doc);
    }

    public async ValueTask SaveLoyaltyWallets(IReadOnlyList<LoyaltyWallet> toSave)
    {
        foreach (var wallet in toSave)
            await SaveLoyaltyWallet(wallet);
    }

    private static (string Key, WalletDocument Doc) ToDocument(LoyaltyWallet wallet) =>
        wallet switch
        {
            LoyaltyWallet.Active w => (w.WalletNumber.Value, new WalletDocument.ActiveOrDeactivated(
                w.WalletNumber, "Active", w.OwnerId, w.Cadence,
                w.PointsLimit.EarnedPoints.Value, w.PointsLimit.RedeemedPoints.Value,
                w.PointsLimit.RedemptionCount.Value, w.PointsLimit.MaxRedemptionCount.Value,
                [.. w.Access.Members])),
            LoyaltyWallet.Deactivated w => (w.WalletNumber.Value, new WalletDocument.ActiveOrDeactivated(
                w.WalletNumber, "Deactivated", w.OwnerId, w.Cadence,
                w.PointsLimit.EarnedPoints.Value, w.PointsLimit.RedeemedPoints.Value,
                w.PointsLimit.RedemptionCount.Value, w.PointsLimit.MaxRedemptionCount.Value,
                [.. w.Access.Members])),
            LoyaltyWallet.Closed w => (w.WalletNumber.Value, new WalletDocument.Closed(w.WalletNumber)),
            LoyaltyWallet.NotExisting => throw new InvalidOperationException("Cannot persist a non-existing wallet"),
            _ => throw new ArgumentOutOfRangeException(nameof(wallet))
        };

    private static LoyaltyWallet FromDocument(WalletDocument doc) =>
        doc switch
        {
            WalletDocument.ActiveOrDeactivated d =>
                d.Status == "Active"
                    ? new LoyaltyWallet.Active(
                        d.WalletNumber, d.OwnerId,
                        LoyaltyPointsLimit.Of(
                            LoyaltyPoints.Of(d.EarnedPoints), LoyaltyPoints.Of(d.RedeemedPoints),
                            RedemptionLimit.Of(d.RedemptionCount), RedemptionLimit.Of(d.MaxRedemptionCount)),
                        d.Cadence,
                        WalletAccess.Of([.. d.AccessMembers]))
                    : new LoyaltyWallet.Deactivated(
                        d.WalletNumber, d.OwnerId,
                        LoyaltyPointsLimit.Of(
                            LoyaltyPoints.Of(d.EarnedPoints), LoyaltyPoints.Of(d.RedeemedPoints),
                            RedemptionLimit.Of(d.RedemptionCount), RedemptionLimit.Of(d.MaxRedemptionCount)),
                        d.Cadence,
                        WalletAccess.Of([.. d.AccessMembers])),
            WalletDocument.Closed d => new LoyaltyWallet.Closed(d.WalletNumber),
            _ => throw new ArgumentOutOfRangeException(nameof(doc))
        };
}
