namespace ConsistencyClass.Tests.Membership;

using ConsistencyClass.LoyaltyWallets;
using ConsistencyClass.Membership;

public class TierProgramTests
{
    [Theory]
    [InlineData(Tier.Standard, 1.0, RedemptionCadence.Weekly, 3)]
    [InlineData(Tier.Silver, 0.97, RedemptionCadence.Weekly, 5)]
    [InlineData(Tier.Gold, 0.95, RedemptionCadence.Monthly, 10)]
    [InlineData(Tier.Platinum, 0.9, RedemptionCadence.Monthly, 20)]
    public void MapsToItsBenefitRateCadenceAndRedemptionLimit(
        Tier tier, double benefitRate, RedemptionCadence cadence, int maxRedemptionCount)
    {
        var program = TierPrograms.For(tier);

        program.BenefitRate.ShouldBe(benefitRate);
        program.Cadence.ShouldBe(cadence);
        program.MaxRedemptionCount.ShouldBe(RedemptionLimit.Of(maxRedemptionCount));
    }
}
