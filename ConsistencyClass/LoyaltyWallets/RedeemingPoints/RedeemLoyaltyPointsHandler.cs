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

        var burned = wallet is LoyaltyWallet.Active active
            ? BenefitPolicy.Apply(command.Points, await getMemberTier(active.OwnerId))
            : command.Points;

        var (state, @event) = RedeemLoyaltyPoints(command with { Burned = burned }, wallet);
        await saveLoyaltyWallet(state, [@event]);
    }
}
