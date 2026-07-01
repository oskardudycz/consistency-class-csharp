namespace ConsistencyClass.LoyaltyWallets.EarningPoints;

using ConsistencyClass.Membership;

public record Money(decimal Value)
{
    public static Money Of(decimal value) => new(value);
}

public enum Channel { InStore, MobileApp, Web }

public enum EarnRole { Referrer, Operator }

public record Beneficiary(MemberId Member, EarnRole Role);

public record PurchaseRecorded(
    Money PurchaseValue,
    MemberId Buyer,
    Channel Channel,
    DateTime At,
    IReadOnlyList<LoyaltyPoints> PromotionBonuses,
    IReadOnlyList<Beneficiary> Beneficiaries);

public record PurchaseInformation(
    Money PurchaseValue,
    MemberId Buyer,
    Channel Channel,
    DateTime At,
    IReadOnlyList<LoyaltyPoints> PromotionBonuses,
    IReadOnlyList<Beneficiary> Beneficiaries,
    Tier Tier)
{
    public static PurchaseInformation From(PurchaseRecorded recorded, Tier tier) =>
        new(recorded.PurchaseValue, recorded.Buyer, recorded.Channel, recorded.At,
            recorded.PromotionBonuses, recorded.Beneficiaries, tier);
}
