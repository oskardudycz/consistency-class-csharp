namespace ConsistencyClass.LoyaltyWallets.RedemptionWindows.EarningPoints;

using ConsistencyClass.LoyaltyWallets.RedemptionWindows;
using ConsistencyClass.Membership;
using ConsistencyClass.Membership.MemberDirectory;
using static ConsistencyClass.LoyaltyWallets.RedemptionWindows.RedemptionWindowCommand;
using static ConsistencyClass.LoyaltyWallets.RedemptionWindows.RedemptionWindowDecider;

public class EarnLoyaltyPointsHandler(
    CurrentWindowsByOwners currentWindowsByOwners,
    GetRedemptionWindow getRedemptionWindow,
    SaveRedemptionWindows saveRedemptionWindows,
    GetMemberTier getMemberTier)
{
    public async ValueTask Handle(PurchaseRecorded @event)
    {
        var breakdown = PointsCalculator.Calculate(
            PurchaseInformation.From(@event, await getMemberTier(@event.Buyer)));

        var found = await currentWindowsByOwners(
            breakdown.Components.Select(c => c.Member).ToList());

        var openByOwner = found
            .Where(w => w.Open)
            .ToDictionary(w => w.OwnerId);

        var updates = new List<RedemptionWindowUpdate>();

        foreach (var component in breakdown.Components)
        {
            if (!openByOwner.TryGetValue(component.Member, out var current))
                continue;

            var window = await getRedemptionWindow(current.WalletNumber, current.WindowNumber);
            var earned = EarnLoyaltyPoints(
                new EarnLoyaltyPoints(current.WalletNumber, component.Amount, @event.At),
                window);
            updates.Add(new RedemptionWindowUpdate(current.WalletNumber, current.WindowNumber, [earned]));
        }

        await saveRedemptionWindows(updates);
    }
}
