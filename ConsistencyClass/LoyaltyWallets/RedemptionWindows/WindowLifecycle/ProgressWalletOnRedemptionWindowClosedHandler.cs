namespace ConsistencyClass.LoyaltyWallets.RedemptionWindows.WindowLifecycle;

using static LoyaltyWalletCommand;
using static LoyaltyWalletDecider;

public class ProgressWalletOnRedemptionWindowClosedHandler(
    GetLoyaltyWallet getLoyaltyWallet,
    SaveLoyaltyWallet saveLoyaltyWallet)
{
    public async ValueTask Handle(RedemptionWindowEvent.RedemptionWindowClosed @event)
    {
        var wallet = await getLoyaltyWallet(@event.WalletNumber);
        var events = OpenNextRedemptionWindow(
            new OpenNextRedemptionWindow(
                @event.WalletNumber,
                @event.WindowNumber,
                @event.ClosingBalance),
            wallet);
        await saveLoyaltyWallet(@event.WalletNumber, events);
    }
}
