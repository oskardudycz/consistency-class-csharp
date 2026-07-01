namespace ConsistencyClass.LoyaltyWallets.RedeemingPoints;

using static LoyaltyWalletCommand;
using static LoyaltyWalletDecider;

public class SetRedemptionCadenceHandler(GetLoyaltyWallet getLoyaltyWallet, SaveLoyaltyWallet saveLoyaltyWallet)
{
    public async ValueTask Handle(SetRedemptionCadence command)
    {
        var (state, @event) = SetRedemptionCadence(command, await getLoyaltyWallet(command.WalletNumber));

        await saveLoyaltyWallet(state, [@event]);
    }
}
