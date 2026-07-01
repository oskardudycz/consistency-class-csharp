namespace ConsistencyClass.LoyaltyWallets.WalletLifecycle;

using static LoyaltyWalletCommand;
using static LoyaltyWalletDecider;

public class SetRedemptionCadenceHandler(GetLoyaltyWallet getLoyaltyWallet, SaveLoyaltyWallet saveLoyaltyWallet)
{
    public async ValueTask Handle(SetRedemptionCadence command)
    {
        var @event = SetRedemptionCadence(command, await getLoyaltyWallet(command.WalletNumber));

        await saveLoyaltyWallet(command.WalletNumber, [@event]);
    }
}
