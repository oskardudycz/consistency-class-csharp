namespace ConsistencyClass.LoyaltyWallets.RedemptionWindows.WindowLifecycle;

using static RedemptionWindowCommand;
using static RedemptionWindowDecider;

public class CloseRedemptionWindowHandler(
    CurrentWindowOf currentWindowOf,
    GetRedemptionWindow getRedemptionWindow,
    SaveRedemptionWindow saveRedemptionWindow)
{
    public async ValueTask Handle(CloseRedemptionWindow command)
    {
        var current = await currentWindowOf(command.WalletNumber);
        if (current is null)
            return;

        var window = await getRedemptionWindow(command.WalletNumber, current.WindowNumber);
        var events = CloseRedemptionWindow(command, window);
        await saveRedemptionWindow(command.WalletNumber, current.WindowNumber, events);
    }
}
