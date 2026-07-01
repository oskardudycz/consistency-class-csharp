namespace ConsistencyClass.LoyaltyWallets.RedemptionWindows.WindowLifecycle;

using static RedemptionWindowCommand;
using static RedemptionWindowDecider;

public class CloseRedemptionWindowOnWalletDeactivatedHandler(
    CurrentWindowOf currentWindowOf,
    GetRedemptionWindow getRedemptionWindow,
    SaveRedemptionWindow saveRedemptionWindow)
{
    public async ValueTask Handle(LoyaltyWalletEvent.WalletDeactivated @event)
    {
        var current = await currentWindowOf(@event.WalletNumber);
        if (current is null)
            return;

        var window = await getRedemptionWindow(@event.WalletNumber, current.WindowNumber);
        var events = CloseRedemptionWindow(
            new CloseRedemptionWindow(@event.WalletNumber, DateTime.UtcNow),
            window);
        await saveRedemptionWindow(@event.WalletNumber, current.WindowNumber, events);
    }
}
