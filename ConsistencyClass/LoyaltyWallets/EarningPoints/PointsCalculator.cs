namespace ConsistencyClass.LoyaltyWallets.EarningPoints;

using ConsistencyClass.Membership;

public enum ComponentRole { Buyer, Referrer, Operator }

public record PointComponent(MemberId Member, ComponentRole Role, LoyaltyPoints Amount);

public record ComponentBreakdown(IReadOnlyList<PointComponent> Components);

public static class PointsCalculator
{
    private static double Rate(decimal purchaseValue) =>
        purchaseValue < 100 ? 0.05 : purchaseValue < 500 ? 0.07 : 0.1;

    private static readonly Dictionary<Tier, double> TierMultiplier = new()
    {
        [Tier.Standard] = 1.0,
        [Tier.Silver] = 1.1,
        [Tier.Gold] = 1.25,
        [Tier.Platinum] = 1.5,
    };

    private static readonly Dictionary<Channel, double> ChannelMultiplier = new()
    {
        [Channel.InStore] = 1.0,
        [Channel.MobileApp] = 1.2,
        [Channel.Web] = 1.1,
    };

    private static readonly Dictionary<EarnRole, double> ShareOfBuyer = new()
    {
        [EarnRole.Referrer] = 0.1,
        [EarnRole.Operator] = 0.05,
    };

    private static bool IsEvening(DateTime at) =>
        at.Hour >= 18 && at.Hour < 22;

    public static LoyaltyPoints BuyerPoints(PurchaseInformation purchase)
    {
        var points = (double)purchase.PurchaseValue.Value * Rate(purchase.PurchaseValue.Value);
        points *= TierMultiplier[purchase.Tier];
        points *= ChannelMultiplier[purchase.Channel];
        if (IsEvening(purchase.At))
            points *= 1.5;
        points += purchase.PromotionBonuses.Sum(b => (double)b.Value);
        return LoyaltyPoints.Of((int)Math.Round(points));
    }

    public static ComponentBreakdown Calculate(PurchaseInformation purchase)
    {
        var basePoints = BuyerPoints(purchase);
        var components = new List<PointComponent>
        {
            new(purchase.Buyer, ComponentRole.Buyer, basePoints)
        };
        components.AddRange(purchase.Beneficiaries.Select(b =>
            new PointComponent(
                b.Member,
                b.Role == EarnRole.Referrer ? ComponentRole.Referrer : ComponentRole.Operator,
                LoyaltyPoints.Of((int)Math.Round(basePoints.Value * ShareOfBuyer[b.Role]))
            )));
        return new ComponentBreakdown(components);
    }
}
