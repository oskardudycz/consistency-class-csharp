namespace ConsistencyClass.LoyaltyWallets.WalletLifecycle;

using static LoyaltyWalletCommand;
using static LoyaltyWalletDecider;

public class DeactivateWalletHandler(GetLoyaltyWallet getLoyaltyWallet, SaveLoyaltyWallet saveLoyaltyWallet)
{
    public async ValueTask Handle(DeactivateWallet command)
    {
        var events = DeactivateWallet(await getLoyaltyWallet(command.WalletNumber));

        await saveLoyaltyWallet(command.WalletNumber, events);
    }
}
