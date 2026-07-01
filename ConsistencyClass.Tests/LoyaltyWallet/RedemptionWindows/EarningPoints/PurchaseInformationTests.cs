namespace ConsistencyClass.Tests.LoyaltyWallets.RedemptionWindows.EarningPoints;

using ConsistencyClass.LoyaltyWallets;
using ConsistencyClass.LoyaltyWallets.RedemptionWindows.EarningPoints;
using ConsistencyClass.Membership;

public class PurchaseInformationTests
{
    [Fact]
    public void CarriesEveryRecordedFieldThroughAndAttachesTheTier()
    {
        var recorded = new PurchaseRecorded(
            Money.Of(1000),
            MemberId.Random(),
            Channel.MobileApp,
            new DateTime(2026, 6, 23, 19, 0, 0),
            [LoyaltyPoints.Of(10)],
            [new Beneficiary(MemberId.Random(), EarnRole.Referrer)]);

        var info = PurchaseInformation.From(recorded, Tier.Gold);

        info.PurchaseValue.ShouldBe(recorded.PurchaseValue);
        info.Buyer.ShouldBe(recorded.Buyer);
        info.Channel.ShouldBe(recorded.Channel);
        info.At.ShouldBe(recorded.At);
        info.PromotionBonuses.ShouldBe(recorded.PromotionBonuses);
        info.Beneficiaries.ShouldBe(recorded.Beneficiaries);
        info.Tier.ShouldBe(Tier.Gold);
    }
}
