namespace ConsistencyClass.LoyaltyWallets.WalletLifecycle;

using ConsistencyClass.Membership;
using ConsistencyClass.Membership.MemberDirectory;

public class OpenWalletOnMemberVerifiedHandler(SaveLoyaltyWallet saveLoyaltyWallet)
{
    public async ValueTask Handle(MemberVerified @event)
    {
        var tierProgram = TierPrograms.For(@event.Tier);
        var walletNumber = WalletNumber.ForOwner(@event.MemberId);
        await saveLoyaltyWallet(LoyaltyWallet.Open(walletNumber, @event.MemberId, tierProgram.Cadence, tierProgram.MaxRedemptionCount));
    }
}
