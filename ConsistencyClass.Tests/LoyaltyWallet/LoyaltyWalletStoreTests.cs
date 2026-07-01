using ConsistencyClass.LoyaltyWallets;
using ConsistencyClass.Membership;
using static ConsistencyClass.LoyaltyWallets.LoyaltyWalletCommand;
using static ConsistencyClass.LoyaltyWallets.LoyaltyWalletDecider;
using WalletDetailsDocument = ConsistencyClass.LoyaltyWallets.WalletDetails.WalletDetails;

namespace ConsistencyClass.Tests.LoyaltyWallets;

public class LoyaltyWalletStoreTests
{
    private readonly DatabaseCollection<WalletDetailsDocument> _wallets = Database.Collection<WalletDetailsDocument>();
    private readonly LoyaltyWalletStore _store;

    public LoyaltyWalletStoreTests() => _store = new LoyaltyWalletStore(_wallets);

    private static LoyaltyWallet.Active ActiveWallet(MemberId ownerId) =>
        LoyaltyWallet.Open(WalletNumber.Random(), ownerId, RedemptionCadence.Monthly, RedemptionLimit.Of(10));

    private async Task<LoyaltyWallet.Active> Enroll(MemberId ownerId)
    {
        var wallet = ActiveWallet(ownerId);
        await _store.SaveLoyaltyWallet(wallet.WalletNumber, OpenLoyaltyWallet(
            new OpenLoyaltyWallet(wallet.WalletNumber, ownerId, RedemptionLimit.Of(10), RedemptionCadence.Monthly),
            LoyaltyWallet.Initial()));
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
        private readonly DatabaseCollection<WalletDetailsDocument> _wallets = Database.Collection<WalletDetailsDocument>();
        private readonly LoyaltyWalletStore _store;

        public FindLoyaltyWalletsByOwners() => _store = new LoyaltyWalletStore(_wallets);

        private async Task<LoyaltyWallet.Active> Enroll(MemberId ownerId)
        {
            var wallet = LoyaltyWallet.Open(WalletNumber.Random(), ownerId, RedemptionCadence.Monthly, RedemptionLimit.Of(10));
            await _store.SaveLoyaltyWallet(wallet.WalletNumber, OpenLoyaltyWallet(
                new OpenLoyaltyWallet(wallet.WalletNumber, ownerId, RedemptionLimit.Of(10), RedemptionCadence.Monthly),
                LoyaltyWallet.Initial()));
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
        private readonly DatabaseCollection<WalletDetailsDocument> _wallets = Database.Collection<WalletDetailsDocument>();
        private readonly LoyaltyWalletStore _store;

        public SaveLoyaltyWallets() => _store = new LoyaltyWalletStore(_wallets);

        private async Task<LoyaltyWallet.Active> Enroll(MemberId ownerId)
        {
            var wallet = LoyaltyWallet.Open(WalletNumber.Random(), ownerId, RedemptionCadence.Monthly, RedemptionLimit.Of(10));
            await _store.SaveLoyaltyWallet(wallet.WalletNumber, OpenLoyaltyWallet(
                new OpenLoyaltyWallet(wallet.WalletNumber, ownerId, RedemptionLimit.Of(10), RedemptionCadence.Monthly),
                LoyaltyWallet.Initial()));
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
                new LoyaltyWalletUpdate(first.WalletNumber, [
                    EarnLoyaltyPoints(new EarnLoyaltyPoints(first.WalletNumber, LoyaltyPoints.Of(30), new DateTime(2026, 6, 23, 12, 0, 0, DateTimeKind.Utc)), first)
                ]),
                new LoyaltyWalletUpdate(second.WalletNumber, [
                    EarnLoyaltyPoints(new EarnLoyaltyPoints(second.WalletNumber, LoyaltyPoints.Of(70), new DateTime(2026, 6, 23, 12, 0, 0, DateTimeKind.Utc)), second)
                ]),
            ]);

            // then both wallets reflect their new balance
            var reloaded = await _store.FindLoyaltyWalletsByOwners([firstOwner, secondOwner]);
            var byOwner = reloaded.ToDictionary(w => OwnerOf(w));
            BalanceOf(byOwner[firstOwner]).ShouldBe(30);
            BalanceOf(byOwner[secondOwner]).ShouldBe(70);
        }
    }

    public class Projections
    {
        private static readonly DateTime At = new(2026, 6, 23, 12, 0, 0, DateTimeKind.Utc);

        private readonly LoyaltyWalletStore _store = new(Database.Collection<WalletDetailsDocument>());

        [Fact]
        public async Task WritesTheActivityReportAndMonthlySummaryAlongsideTheWallet()
        {
            // given an opened wallet earning and redeeming within one window
            var owner = MemberId.Random();
            var walletNumber = WalletNumber.Random();

            var opened = OpenLoyaltyWallet(
                new OpenLoyaltyWallet(walletNumber, owner, RedemptionLimit.Of(5), RedemptionCadence.Monthly),
                LoyaltyWallet.Initial());
            await _store.SaveLoyaltyWallet(walletNumber, opened);

            var earned = EarnLoyaltyPoints(
                new EarnLoyaltyPoints(walletNumber, LoyaltyPoints.Of(100), At),
                await _store.GetLoyaltyWallet(walletNumber));
            await _store.SaveLoyaltyWallet(walletNumber, [earned]);

            var redeemed = RedeemLoyaltyPoints(
                new RedeemLoyaltyPoints(walletNumber, owner, LoyaltyPoints.Of(40), At),
                await _store.GetLoyaltyWallet(walletNumber));
            await _store.SaveLoyaltyWallet(walletNumber, [redeemed]);

            var reset = ResetRedemptionWindow(
                new ResetRedemptionWindow(walletNumber, At),
                await _store.GetLoyaltyWallet(walletNumber));
            await _store.SaveLoyaltyWallet(walletNumber, [reset]);

            // then the activity report groups the activity into windows
            var report = await _store.ActivityReports.Find(walletNumber.Value);
            report.ShouldNotBeNull();
            report.OwnerId.ShouldBe(owner);
            report.ClosedWindows.Count.ShouldBe(1);
            report.ClosedWindows[0].Earned.ShouldBe(100);
            report.ClosedWindows[0].Redeemed.ShouldBe(40);
            report.ClosedWindows[0].Burned.ShouldBe(40);
            report.ClosedWindows[0].RedemptionCount.ShouldBe(1);
            report.CurrentWindow.WindowNumber.ShouldBe(2);
            report.CurrentWindow.HadActivity.ShouldBeFalse();

            // and the monthly summary aggregates the month's totals
            var summary = await _store.MonthlySummaries.Find($"{walletNumber.Value}:2026-06");
            summary.ShouldNotBeNull();
            summary.TotalEarned.ShouldBe(100);
            summary.TotalRedeemed.ShouldBe(40);
            summary.TotalBurned.ShouldBe(40);
            summary.RedemptionCount.ShouldBe(1);
            summary.WindowsClosed.ShouldBe(1);
        }
    }
}
