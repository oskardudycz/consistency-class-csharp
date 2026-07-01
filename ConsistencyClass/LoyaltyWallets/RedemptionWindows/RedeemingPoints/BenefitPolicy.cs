namespace ConsistencyClass.LoyaltyWallets.RedemptionWindows.RedeemingPoints;

using ConsistencyClass.Membership;

public static class BenefitPolicy
{
    public static LoyaltyPoints Apply(LoyaltyPoints redeemed, Tier tier) =>
        LoyaltyPoints.Of((int)Math.Round(redeemed.Value * TierPrograms.For(tier).BenefitRate));
}
