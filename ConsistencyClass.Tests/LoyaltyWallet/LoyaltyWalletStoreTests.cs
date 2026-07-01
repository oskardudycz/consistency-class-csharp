using ConsistencyClass.LoyaltyWallets;
using ConsistencyClass.Membership;

namespace ConsistencyClass.Tests.LoyaltyWallets;

public class LoyaltyWalletStoreTests
{
    private readonly DatabaseCollection<WalletDocument> _wallets = Database.Collection<WalletDocument>();
    private readonly LoyaltyWalletStore _store;

    public LoyaltyWalletStoreTests() => _store = new LoyaltyWalletStore(_wallets);

    private static LoyaltyWallet.Active ActiveWallet(MemberId ownerId) =>
        LoyaltyWallet.Open(WalletNumber.Random(), ownerId, RedemptionCadence.Monthly, RedemptionLimit.Of(10));

    private async Task<LoyaltyWallet.Active> Enroll(MemberId ownerId)
    {
        var wallet = ActiveWallet(ownerId);
        await _store.SaveLoyaltyWallet(wallet);
        return wallet;
    }

    private static MemberId OwnerOf(LoyaltyWallet wallet) =>
        ((LoyaltyWallet.Active)wallet).OwnerId;

    private static int BalanceOf(LoyaltyWallet wallet) =>
        ((LoyaltyWallet.Active)wallet).PointsLimit.AvailablePoints.Value;

    [Fact]
    public async Task RoundTripsAWalletByItsWalletNumber()
    {
        // given
        var wallet = await Enroll(MemberId.Random());

        // then it can be loaded back by its number
        var loaded = await _store.GetLoyaltyWallet(wallet.WalletNumber);
        loaded.ShouldBeOfType<LoyaltyWallet.Active>()
            .WalletNumber.ShouldBe(wallet.WalletNumber);
    }

    public class FindLoyaltyWalletsByOwners
    {
        private readonly DatabaseCollection<WalletDocument> _wallets = Database.Collection<WalletDocument>();
        private readonly LoyaltyWalletStore _store;

        public FindLoyaltyWalletsByOwners() => _store = new LoyaltyWalletStore(_wallets);

        private async Task<LoyaltyWallet.Active> Enroll(MemberId ownerId)
        {
            var wallet = LoyaltyWallet.Open(WalletNumber.Random(), ownerId, RedemptionCadence.Monthly, RedemptionLimit.Of(10));
            await _store.SaveLoyaltyWallet(wallet);
            return wallet;
        }

        private static MemberId OwnerOf(LoyaltyWallet wallet) => ((LoyaltyWallet.Active)wallet).OwnerId;

        [Fact]
        public async Task ReturnsAListOfTheRequestedOwnersWallets()
        {
            // given
            var first = MemberId.Random();
            var second = MemberId.Random();
            await Enroll(first);
            await Enroll(second);

            // when
            var found = await _store.FindLoyaltyWalletsByOwners([first, second]);

            // then
            found.Count.ShouldBe(2);
            found.Select(OwnerOf).ShouldContain(first);
            found.Select(OwnerOf).ShouldContain(second);
        }

        [Fact]
        public async Task OmitsOwnersWithoutAWallet()
        {
            // given
            var enrolled = MemberId.Random();
            var unknown = MemberId.Random();
            await Enroll(enrolled);

            // when
            var found = await _store.FindLoyaltyWalletsByOwners([enrolled, unknown]);

            // then
            found.Count.ShouldBe(1);
            OwnerOf(found[0]).ShouldBe(enrolled);
        }
    }

    public class SaveLoyaltyWallets
    {
        private readonly DatabaseCollection<WalletDocument> _wallets = Database.Collection<WalletDocument>();
        private readonly LoyaltyWalletStore _store;

        public SaveLoyaltyWallets() => _store = new LoyaltyWalletStore(_wallets);

        private async Task<LoyaltyWallet.Active> Enroll(MemberId ownerId)
        {
            var wallet = LoyaltyWallet.Open(WalletNumber.Random(), ownerId, RedemptionCadence.Monthly, RedemptionLimit.Of(10));
            await _store.SaveLoyaltyWallet(wallet);
            return wallet;
        }

        private static MemberId OwnerOf(LoyaltyWallet wallet) => ((LoyaltyWallet.Active)wallet).OwnerId;
        private static int BalanceOf(LoyaltyWallet wallet) => ((LoyaltyWallet.Active)wallet).PointsLimit.AvailablePoints.Value;

        [Fact]
        public async Task PersistsEveryWalletInTheBatchInOneCall()
        {
            // given two enrolled wallets earning points
            var firstOwner = MemberId.Random();
            var secondOwner = MemberId.Random();
            var first = await Enroll(firstOwner);
            var second = await Enroll(secondOwner);

            // when both are credited in a single batch
            await _store.SaveLoyaltyWallets([
                first with { PointsLimit = first.PointsLimit.Earn(LoyaltyPoints.Of(30)) },
                second with { PointsLimit = second.PointsLimit.Earn(LoyaltyPoints.Of(70)) },
            ]);

            // then both wallets reflect their new balance
            var reloaded = await _store.FindLoyaltyWalletsByOwners([firstOwner, secondOwner]);
            var byOwner = reloaded.ToDictionary(w => OwnerOf(w));
            BalanceOf(byOwner[firstOwner]).ShouldBe(30);
            BalanceOf(byOwner[secondOwner]).ShouldBe(70);
        }
    }
}
