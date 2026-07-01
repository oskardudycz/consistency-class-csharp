namespace ConsistencyClass.LoyaltyWallets;

public record LoyaltyPoints(int Value)
{
    public static readonly LoyaltyPoints Zero = new(0);

    public static LoyaltyPoints Of(int value) => new(value);
}

public record RedemptionLimit(int Value)
{
    public static readonly RedemptionLimit Zero = new(0);

    public static RedemptionLimit Of(int value) => new(value);
}

public record LoyaltyPointsLimit(
    LoyaltyPoints EarnedPoints,
    LoyaltyPoints RedeemedPoints,
    LoyaltyPoints AvailablePoints,
    RedemptionLimit RedemptionCount,
    RedemptionLimit MaxRedemptionCount)
{
    public static LoyaltyPointsLimit Initial(RedemptionLimit maxRedemptionCount) =>
        new(LoyaltyPoints.Zero, LoyaltyPoints.Zero, LoyaltyPoints.Zero,
            RedemptionLimit.Zero, maxRedemptionCount);

    public static LoyaltyPointsLimit Of(
        LoyaltyPoints earnedPoints,
        LoyaltyPoints redeemedPoints,
        RedemptionLimit redemptionCount,
        RedemptionLimit maxRedemptionCount)
    {
        var available = LoyaltyPoints.Of(earnedPoints.Value - redeemedPoints.Value);
        return new(earnedPoints, redeemedPoints, available, redemptionCount, maxRedemptionCount);
    }

    public LoyaltyPointsLimit Earn(LoyaltyPoints points) =>
        this with
        {
            EarnedPoints = new LoyaltyPoints(EarnedPoints.Value + points.Value),
            AvailablePoints = new LoyaltyPoints(AvailablePoints.Value + points.Value)
        };

    public int RedemptionsLeft => MaxRedemptionCount.Value - RedemptionCount.Value;

    public LoyaltyPointsLimit Redeem(LoyaltyPoints points) =>
        this with
        {
            RedeemedPoints = new LoyaltyPoints(RedeemedPoints.Value + points.Value),
            AvailablePoints = new LoyaltyPoints(AvailablePoints.Value - points.Value),
            RedemptionCount = new RedemptionLimit(RedemptionCount.Value + 1)
        };

    public LoyaltyPointsLimit ResetRedemptionCount() =>
        this with { RedemptionCount = RedemptionLimit.Zero };
}
