namespace ConsistencyClass.LoyaltyWallets.RedemptionWindows.WindowLifecycle;

using static RedemptionWindowCommand;
using static RedemptionWindowDecider;

public class OpenRedemptionWindowOnProgressedHandler(
    GetRedemptionWindow getRedemptionWindow,
    SaveRedemptionWindow saveRedemptionWindow)
{
    public async ValueTask Handle(LoyaltyWalletEvent.RedemptionWindowProgressed @event)
    {
        var window = await getRedemptionWindow(@event.WalletNumber, @event.WindowNumber);
        var events = OpenRedemptionWindow(
            new OpenRedemptionWindow(
                @event.WalletNumber,
                @event.OwnerId,
                @event.WindowNumber,
                @event.OpeningBalance,
                @event.MaxRedemptionCount,
                @event.Access),
            window);
        await saveRedemptionWindow(@event.WalletNumber, @event.WindowNumber, events, true);
    }
}
