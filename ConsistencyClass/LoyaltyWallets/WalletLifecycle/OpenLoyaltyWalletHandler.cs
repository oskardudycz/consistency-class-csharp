namespace ConsistencyClass.LoyaltyWallets.WalletLifecycle;

using static LoyaltyWalletCommand;
using static LoyaltyWalletDecider;

public class OpenLoyaltyWalletHandler(GetLoyaltyWallet getLoyaltyWallet, SaveLoyaltyWallet saveLoyaltyWallet)
{
    public async ValueTask Handle(OpenLoyaltyWallet command) =>
        await saveLoyaltyWallet(OpenLoyaltyWallet(command, await getLoyaltyWallet(command.WalletNumber)));
}
