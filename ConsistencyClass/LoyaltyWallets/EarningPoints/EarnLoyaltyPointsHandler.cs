namespace ConsistencyClass.LoyaltyWallets.EarningPoints;

using ConsistencyClass.Membership;
using ConsistencyClass.Membership.MemberDirectory;
using static LoyaltyWalletCommand;
using static LoyaltyWalletDecider;

public class EarnLoyaltyPointsHandler(
    FindLoyaltyWalletsByOwners findLoyaltyWalletsByOwners,
    SaveLoyaltyWallets saveLoyaltyWallets,
    GetMemberTier getMemberTier)
{
    public async ValueTask Handle(PurchaseRecorded @event)
    {
        var breakdown = PointsCalculator.Calculate(
            PurchaseInformation.From(@event, await getMemberTier(@event.Buyer)));

        var found = await findLoyaltyWalletsByOwners(
            breakdown.Components.Select(c => c.Member).ToList());

        var activeByOwner = found
            .OfType<LoyaltyWallet.Active>()
            .ToDictionary(w => w.OwnerId);

        var updates = breakdown.Components
            .Where(c => activeByOwner.ContainsKey(c.Member))
            .Select(c =>
            {
                var wallet = activeByOwner[c.Member];
                return (LoyaltyWallet)EarnLoyaltyPoints(
                    new EarnLoyaltyPoints(wallet.WalletNumber, c.Amount), wallet);
            })
            .ToList();

        await saveLoyaltyWallets(updates);
    }
}
