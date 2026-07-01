namespace ConsistencyClass.LoyaltyWallets.RedeemingPoints;

using ConsistencyClass.Membership.MemberDirectory;
using static LoyaltyWalletCommand;
using static LoyaltyWalletDecider;

public class RedeemLoyaltyPointsHandler(
    GetLoyaltyWallet getLoyaltyWallet,
    SaveLoyaltyWallet saveLoyaltyWallet,
    GetMemberTier getMemberTier)
{
    public async ValueTask Handle(RedeemLoyaltyPoints command)
    {
        var wallet = await getLoyaltyWallet(command.WalletNumber);

        var points = wallet is LoyaltyWallet.Active active
            ? BenefitPolicy.Apply(command.Points, await getMemberTier(active.OwnerId))
            : command.Points;

        await saveLoyaltyWallet(RedeemLoyaltyPoints(command with { Points = points }, wallet));
    }
}
