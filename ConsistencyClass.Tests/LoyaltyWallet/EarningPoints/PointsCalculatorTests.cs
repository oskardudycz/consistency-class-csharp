namespace ConsistencyClass.Tests.LoyaltyWallets.EarningPoints;

using ConsistencyClass.LoyaltyWallets;
using ConsistencyClass.LoyaltyWallets.EarningPoints;
using ConsistencyClass.Membership;

public class PointsCalculatorFixture
{
    public readonly MemberId Buyer = MemberId.Random();
    public readonly MemberId Referrer = MemberId.Random();
    public readonly MemberId Operator = MemberId.Random();

    public PurchaseInformation Purchase(
        Money? purchaseValue = null,
        Tier? tier = null,
        Channel? channel = null,
        DateTime? at = null,
        IReadOnlyList<LoyaltyPoints>? promotionBonuses = null,
        IReadOnlyList<Beneficiary>? beneficiaries = null) =>
        new(
            purchaseValue ?? Money.Of(1000),
            Buyer,
            channel ?? Channel.InStore,
            at ?? new DateTime(2026, 6, 23, 12, 0, 0),
            promotionBonuses ?? [],
            beneficiaries ?? [],
            tier ?? Tier.Standard);
}

public class PointsCalculatorTests
{
    public class BuyerPoints(PointsCalculatorFixture fixture): IClassFixture<PointsCalculatorFixture>
    {
        [Theory]
        [InlineData(80, 4)]
        [InlineData(200, 14)]
        [InlineData(1000, 100)]
        public void AppliesTheRateBracket(int value, int expected) =>
            PointsCalculator.BuyerPoints(fixture.Purchase(Money.Of(value))).ShouldBe(LoyaltyPoints.Of(expected));

        [Theory]
        [InlineData(Tier.Standard, 100)]
        [InlineData(Tier.Silver, 110)]
        [InlineData(Tier.Gold, 125)]
        [InlineData(Tier.Platinum, 150)]
        public void AppliesTierMultiplier(Tier tier, int expected) =>
            PointsCalculator.BuyerPoints(fixture.Purchase(tier: tier)).ShouldBe(LoyaltyPoints.Of(expected));

        [Theory]
        [InlineData(Channel.InStore, 100)]
        [InlineData(Channel.MobileApp, 120)]
        [InlineData(Channel.Web, 110)]
        public void AppliesChannelMultiplier(Channel channel, int expected) =>
            PointsCalculator.BuyerPoints(fixture.Purchase(channel: channel)).ShouldBe(LoyaltyPoints.Of(expected));

        [Fact]
        public void AppliesEveningMultiplierBetween18And22()
        {
            PointsCalculator.BuyerPoints(fixture.Purchase(at: new DateTime(2026, 6, 23, 19, 0, 0))).ShouldBe(LoyaltyPoints.Of(150));
            PointsCalculator.BuyerPoints(fixture.Purchase(at: new DateTime(2026, 6, 23, 12, 0, 0))).ShouldBe(LoyaltyPoints.Of(100));
        }

        [Fact]
        public void AddsPromotionBonuses() =>
            PointsCalculator.BuyerPoints(
                fixture.Purchase(promotionBonuses: [LoyaltyPoints.Of(10), LoyaltyPoints.Of(5)])).ShouldBe(LoyaltyPoints.Of(115));
    }

    public class Calculate(PointsCalculatorFixture fixture): IClassFixture<PointsCalculatorFixture>
    {
        [Fact]
        public void CreditsOnlyTheBuyerWhenThereAreNoBeneficiaries()
        {
            var breakdown = PointsCalculator.Calculate(fixture.Purchase());

            breakdown.Components.ShouldBe([
                new PointComponent(fixture.Buyer, ComponentRole.Buyer, LoyaltyPoints.Of(100))
            ]);
        }

        [Fact]
        public void FansOutToOneComponentPerRecipientCountNotPredetermined()
        {
            var breakdown = PointsCalculator.Calculate(fixture.Purchase(beneficiaries: [
                new Beneficiary(fixture.Referrer, EarnRole.Referrer),
                new Beneficiary(fixture.Operator, EarnRole.Operator)
            ]));

            breakdown.Components.Count.ShouldBe(3);
            breakdown.Components.ShouldBe([
                new PointComponent(fixture.Buyer,    ComponentRole.Buyer,     LoyaltyPoints.Of(100)),
                new PointComponent(fixture.Referrer, ComponentRole.Referrer,  LoyaltyPoints.Of(10)),
                new PointComponent(fixture.Operator, ComponentRole.Operator,  LoyaltyPoints.Of(5))
            ]);
        }

        [Fact]
        public void RoundsEachBeneficiaryShare()
        {
            // buyer earns 14 -> referrer 14 * 0.1 = 1.4 -> 1
            var breakdown = PointsCalculator.Calculate(fixture.Purchase(
                purchaseValue: Money.Of(200),
                beneficiaries: [new Beneficiary(fixture.Referrer, EarnRole.Referrer)]));

            breakdown.Components.ShouldBe([
                new PointComponent(fixture.Buyer,    ComponentRole.Buyer,    LoyaltyPoints.Of(14)),
                new PointComponent(fixture.Referrer, ComponentRole.Referrer, LoyaltyPoints.Of(1))
            ]);
        }
    }
}
