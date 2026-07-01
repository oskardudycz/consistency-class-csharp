namespace ConsistencyClass.LoyaltyWallets.WalletDetails;

using ConsistencyClass.Core.Projections;
using ConsistencyClass.LoyaltyWallets.Access;
using ConsistencyClass.Membership;
using static LoyaltyWalletEvent;

internal record WalletDetails(
    WalletNumber WalletNumber,
    string Status,
    MemberId OwnerId,
    RedemptionCadence Cadence,
    int EarnedPoints,
    int RedeemedPoints,
    int RedemptionCount,
    int MaxRedemptionCount,
    IReadOnlyList<MemberId> AccessMembers)
{
    public static WalletDetails? Evolve(WalletDetails? document, LoyaltyWalletEvent @event) =>
        @event switch
        {
            LoyaltyWalletOpened opened => new WalletDetails(
                opened.WalletNumber,
                "Active",
                opened.OwnerId,
                opened.Cadence,
                opened.EarnedPoints.Value,
                opened.RedeemedPoints.Value,
                RedemptionLimit.Zero.Value,
                opened.MaxRedemptionCount.Value,
                [opened.OwnerId]),
            LoyaltyPointsEarned earned when document is not null =>
                document with { EarnedPoints = document.EarnedPoints + earned.Points.Value },
            LoyaltyPointsRedeemed redeemed when document is not null =>
                document with
                {
                    RedeemedPoints = document.RedeemedPoints + redeemed.Burned.Value,
                    RedemptionCount = document.RedemptionCount + 1
                },
            RedemptionWindowReset when document is not null =>
                document with { RedemptionCount = RedemptionLimit.Zero.Value },
            RedemptionCadenceSet cadenceSet when document is not null =>
                document with { Cadence = cadenceSet.Cadence },
            WalletAccessGranted granted when document is not null =>
                document with { AccessMembers = document.AccessMembers.Append(granted.MemberId).Distinct().ToList() },
            WalletAccessRevoked revoked when document is not null =>
                document with { AccessMembers = document.AccessMembers.Where(id => id != revoked.MemberId).ToList() },
            WalletDeactivated when document is not null =>
                document with { Status = "Deactivated" },
            WalletClosed when document is not null =>
                document with { Status = "Closed" },
            _ => document
        };

    public static Projection<WalletDetails, LoyaltyWalletEvent> Projection(
        DatabaseCollection<WalletDetails> collection) =>
        new(
            collection,
            new HashSet<Type>
            {
                typeof(LoyaltyWalletOpened),
                typeof(LoyaltyPointsEarned),
                typeof(LoyaltyPointsRedeemed),
                typeof(RedemptionWindowReset),
                typeof(RedemptionCadenceSet),
                typeof(WalletAccessGranted),
                typeof(WalletAccessRevoked),
                typeof(WalletDeactivated),
                typeof(WalletClosed)
            },
            WalletNumberOf,
            Evolve);

    public static LoyaltyWallet ToWallet(WalletDetails document) =>
        document.Status switch
        {
            "Active" => new LoyaltyWallet.Active(
                document.WalletNumber,
                document.OwnerId,
                PointsLimit(document),
                document.Cadence,
                WalletAccess.Of([.. document.AccessMembers])),
            "Deactivated" => new LoyaltyWallet.Deactivated(
                document.WalletNumber,
                document.OwnerId,
                PointsLimit(document),
                document.Cadence,
                WalletAccess.Of([.. document.AccessMembers])),
            "Closed" => new LoyaltyWallet.Closed(document.WalletNumber),
            _ => throw new ArgumentOutOfRangeException(nameof(document))
        };

    private static LoyaltyPointsLimit PointsLimit(WalletDetails document) =>
        LoyaltyPointsLimit.Of(
            LoyaltyPoints.Of(document.EarnedPoints),
            LoyaltyPoints.Of(document.RedeemedPoints),
            RedemptionLimit.Of(document.RedemptionCount),
            RedemptionLimit.Of(document.MaxRedemptionCount));

    private static string WalletNumberOf(LoyaltyWalletEvent @event) =>
        @event switch
        {
            LoyaltyWalletOpened e => e.WalletNumber.Value,
            LoyaltyPointsEarned e => e.WalletNumber.Value,
            LoyaltyPointsRedeemed e => e.WalletNumber.Value,
            RedemptionWindowReset e => e.WalletNumber.Value,
            RedemptionCadenceSet e => e.WalletNumber.Value,
            WalletAccessGranted e => e.WalletNumber.Value,
            WalletAccessRevoked e => e.WalletNumber.Value,
            WalletDeactivated e => e.WalletNumber.Value,
            WalletClosed e => e.WalletNumber.Value,
            _ => throw new ArgumentOutOfRangeException(nameof(@event))
        };
}
