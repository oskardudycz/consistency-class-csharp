namespace ConsistencyClass.LoyaltyWallets.RedeemingPoints;

using static LoyaltyWalletCommand;
using static LoyaltyWalletDecider;

public class ResetRedemptionWindowHandler(GetLoyaltyWallet getLoyaltyWallet, SaveLoyaltyWallet saveLoyaltyWallet)
{
    public async ValueTask Handle(ResetRedemptionWindow command) =>
        await saveLoyaltyWallet(ResetRedemptionWindow(await getLoyaltyWallet(command.WalletNumber)));
}
