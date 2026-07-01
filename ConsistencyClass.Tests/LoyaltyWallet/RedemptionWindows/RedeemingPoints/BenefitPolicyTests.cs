namespace ConsistencyClass.Tests.LoyaltyWallets.RedemptionWindows.RedeemingPoints;

using ConsistencyClass.LoyaltyWallets;
using ConsistencyClass.LoyaltyWallets.RedemptionWindows.RedeemingPoints;
using ConsistencyClass.Membership;

public class BenefitPolicyTests
{
    [Theory]
    [InlineData(Tier.Standard, 100, 100)]
    [InlineData(Tier.Silver, 100, 97)]
    [InlineData(Tier.Gold, 100, 95)]
    [InlineData(Tier.Platinum, 100, 90)]
    public void BurnsTheTierRateOfTheRedeemedPoints(Tier tier, int redeemed, int burned) =>
        BenefitPolicy.Apply(LoyaltyPoints.Of(redeemed), tier).ShouldBe(LoyaltyPoints.Of(burned));

    [Fact]
    public void RoundsTheBurnedAmount()
    {
        // 99 * 0.95 = 94.05 -> 94
        BenefitPolicy.Apply(LoyaltyPoints.Of(99), Tier.Gold).ShouldBe(LoyaltyPoints.Of(94));
    }

    [Fact]
    public void NeverBurnsMoreThanRedeemed()
    {
        var tiers = new[] { Tier.Standard, Tier.Silver, Tier.Gold, Tier.Platinum };
        var redeemed = LoyaltyPoints.Of(250);
        foreach (var tier in tiers)
            BenefitPolicy.Apply(redeemed, tier).Value.ShouldBeLessThanOrEqualTo(redeemed.Value);
    }
}
