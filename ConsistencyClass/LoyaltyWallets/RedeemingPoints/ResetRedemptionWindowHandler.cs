namespace ConsistencyClass.LoyaltyWallets.RedeemingPoints;

using static LoyaltyWalletCommand;
using static LoyaltyWalletDecider;

public class ResetRedemptionWindowHandler(GetLoyaltyWallet getLoyaltyWallet, SaveLoyaltyWallet saveLoyaltyWallet)
{
    public async ValueTask Handle(ResetRedemptionWindow command)
    {
        var @event = ResetRedemptionWindow(command, await getLoyaltyWallet(command.WalletNumber));
        await saveLoyaltyWallet(command.WalletNumber, [@event]);
    }
}
