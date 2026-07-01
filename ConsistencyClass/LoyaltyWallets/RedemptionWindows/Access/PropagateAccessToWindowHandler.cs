namespace ConsistencyClass.LoyaltyWallets.RedemptionWindows.Access;

using static RedemptionWindowCommand;
using static RedemptionWindowDecider;

public class PropagateAccessToWindowHandler(
    CurrentWindowOf currentWindowOf,
    GetRedemptionWindow getRedemptionWindow,
    SaveRedemptionWindow saveRedemptionWindow)
{
    public async ValueTask Handle(LoyaltyWalletEvent.WalletAccessGranted @event)
    {
        var current = await currentWindowOf(@event.WalletNumber);
        if (current is null)
            return;

        var window = await getRedemptionWindow(@event.WalletNumber, current.WindowNumber);
        var events = GrantWindowAccess(new GrantWindowAccess(@event.WalletNumber, @event.MemberId), window);
        await saveRedemptionWindow(@event.WalletNumber, current.WindowNumber, events);
    }

    public async ValueTask Handle(LoyaltyWalletEvent.WalletAccessRevoked @event)
    {
        var current = await currentWindowOf(@event.WalletNumber);
        if (current is null)
            return;

        var window = await getRedemptionWindow(@event.WalletNumber, current.WindowNumber);
        var events = RevokeWindowAccess(new RevokeWindowAccess(@event.WalletNumber, @event.MemberId), window);
        await saveRedemptionWindow(@event.WalletNumber, current.WindowNumber, events);
    }
}
