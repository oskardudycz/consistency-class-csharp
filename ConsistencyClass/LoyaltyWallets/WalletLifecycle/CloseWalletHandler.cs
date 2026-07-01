namespace ConsistencyClass.LoyaltyWallets.WalletLifecycle;

using static LoyaltyWalletCommand;
using static LoyaltyWalletDecider;

public class CloseWalletHandler(GetLoyaltyWallet getLoyaltyWallet, SaveLoyaltyWallet saveLoyaltyWallet)
{
    public async ValueTask Handle(CloseWallet command)
    {
        var events = CloseWallet(await getLoyaltyWallet(command.WalletNumber));

        await saveLoyaltyWallet(command.WalletNumber, events);
    }
}
