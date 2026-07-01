using ConsistencyClass.LoyaltyWallets;
using ConsistencyClass.LoyaltyWallets.WalletLifecycle;
using ConsistencyClass.Membership;
using ConsistencyClass.Membership.MemberDirectory;

namespace ConsistencyClass.Tests.LoyaltyWallets.WalletLifecycle;

public class OpenWalletOnMemberVerifiedTests
{
    private readonly LoyaltyWalletStore _store = new(Database.Collection<WalletDocument>());

    [Fact]
    public async Task OpensAnActiveWalletWithTheWindowDerivedFromTheTier()
    {
        var oskar = MemberId.Random();

        // when
        await new OpenWalletOnMemberVerifiedHandler(_store.SaveLoyaltyWallet)
            .Handle(new MemberVerified(oskar, Tier.Gold));

        // then an active wallet is opened for the member, the window from their tier
        var wallets = await _store.FindLoyaltyWalletsByOwners([oskar]);
        var wallet = wallets.ShouldHaveSingleItem().ShouldBeOfType<LoyaltyWallet.Active>();
        wallet.OwnerId.ShouldBe(oskar);
        wallet.Cadence.ShouldBe(RedemptionCadence.Monthly);
        wallet.PointsLimit.RedemptionsLeft.ShouldBe(10);
        wallet.Access.Has(oskar).ShouldBeTrue();
    }
}
