namespace ConsistencyClass.LoyaltyWallets.Access;

using static LoyaltyWalletCommand;
using static LoyaltyWalletDecider;

public class RevokeWalletAccessHandler(GetLoyaltyWallet getLoyaltyWallet, SaveLoyaltyWallet saveLoyaltyWallet)
{
    public async ValueTask Handle(RevokeWalletAccess command)
    {
        var (state, @event) = RevokeWalletAccess(command, await getLoyaltyWallet(command.WalletNumber));

        await saveLoyaltyWallet(state, [@event]);
    }
}
