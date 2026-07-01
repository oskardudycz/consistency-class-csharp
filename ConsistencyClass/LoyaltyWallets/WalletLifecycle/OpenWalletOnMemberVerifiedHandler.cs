namespace ConsistencyClass.LoyaltyWallets.WalletLifecycle;

using ConsistencyClass.Membership;
using ConsistencyClass.Membership.MemberDirectory;
using static LoyaltyWalletCommand;
using static LoyaltyWalletDecider;

public class OpenWalletOnMemberVerifiedHandler(SaveLoyaltyWallet saveLoyaltyWallet)
{
    public async ValueTask Handle(MemberVerified @event)
    {
        var tierProgram = TierPrograms.For(@event.Tier);
        var walletNumber = WalletNumber.ForOwner(@event.MemberId);
        var (state, events) = OpenLoyaltyWallet(
            new OpenLoyaltyWallet(walletNumber, @event.MemberId, tierProgram.MaxRedemptionCount, tierProgram.Cadence),
            LoyaltyWallet.Initial());
        await saveLoyaltyWallet(state, events);
    }
}
