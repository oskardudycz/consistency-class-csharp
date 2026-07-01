namespace ConsistencyClass.Membership;

using ConsistencyClass.LoyaltyWallets;

public enum Tier { Standard, Silver, Gold, Platinum }

public record TierProgram(
    double BenefitRate,
    RedemptionCadence Cadence,
    RedemptionLimit MaxRedemptionCount);

public static class TierPrograms
{
    private static readonly Dictionary<Tier, TierProgram> Programs = new()
    {
        [Tier.Standard] = new TierProgram(1.0, RedemptionCadence.Weekly, RedemptionLimit.Of(3)),
        [Tier.Silver] = new TierProgram(0.97, RedemptionCadence.Weekly, RedemptionLimit.Of(5)),
        [Tier.Gold] = new TierProgram(0.95, RedemptionCadence.Monthly, RedemptionLimit.Of(10)),
        [Tier.Platinum] = new TierProgram(0.9, RedemptionCadence.Monthly, RedemptionLimit.Of(20)),
    };

    public static TierProgram For(Tier tier) => Programs[tier];
}
