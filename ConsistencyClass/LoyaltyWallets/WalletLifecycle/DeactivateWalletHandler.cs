namespace ConsistencyClass.LoyaltyWallets.WalletLifecycle;

using static LoyaltyWalletCommand;
using static LoyaltyWalletDecider;

public class DeactivateWalletHandler(GetLoyaltyWallet getLoyaltyWallet, SaveLoyaltyWallet saveLoyaltyWallet)
{
    public async ValueTask Handle(DeactivateWallet command) =>
        await saveLoyaltyWallet(DeactivateWallet(await getLoyaltyWallet(command.WalletNumber)));
}
