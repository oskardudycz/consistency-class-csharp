namespace ConsistencyClass.LoyaltyWallets.Access;

using static LoyaltyWalletCommand;
using static LoyaltyWalletDecider;

public class GrantWalletAccessHandler(GetLoyaltyWallet getLoyaltyWallet, SaveLoyaltyWallet saveLoyaltyWallet)
{
    public async ValueTask Handle(GrantWalletAccess command)
    {
        var @event = GrantWalletAccess(command, await getLoyaltyWallet(command.WalletNumber));

        await saveLoyaltyWallet(command.WalletNumber, [@event]);
    }
}
