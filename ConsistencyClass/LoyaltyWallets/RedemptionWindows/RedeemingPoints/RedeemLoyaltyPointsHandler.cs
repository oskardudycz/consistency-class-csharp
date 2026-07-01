namespace ConsistencyClass.LoyaltyWallets.RedemptionWindows.RedeemingPoints;

using ConsistencyClass.LoyaltyWallets.RedemptionWindows;
using ConsistencyClass.Membership.MemberDirectory;
using static ConsistencyClass.LoyaltyWallets.RedemptionWindows.RedemptionWindowCommand;
using static ConsistencyClass.LoyaltyWallets.RedemptionWindows.RedemptionWindowDecider;

public class RedeemLoyaltyPointsHandler(
    CurrentWindowOf currentWindowOf,
    GetRedemptionWindow getRedemptionWindow,
    SaveRedemptionWindow saveRedemptionWindow,
    GetMemberTier getMemberTier)
{
    public async ValueTask Handle(RedeemLoyaltyPoints command)
    {
        var current = await currentWindowOf(command.WalletNumber) ?? throw new InvalidOperationException("Redemption window is not open");

        var window = await getRedemptionWindow(command.WalletNumber, current.WindowNumber);

        var burned = BenefitPolicy.Apply(command.Points, await getMemberTier(current.OwnerId));

        var @event = RedeemLoyaltyPoints(command with { Burned = burned }, window);
        await saveRedemptionWindow(command.WalletNumber, current.WindowNumber, [@event]);
    }
}
