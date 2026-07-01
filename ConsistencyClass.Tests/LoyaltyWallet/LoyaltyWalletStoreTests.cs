using ConsistencyClass.LoyaltyWallets;
using ConsistencyClass.LoyaltyWallets.RedemptionWindows;
using ConsistencyClass.Membership;
using static ConsistencyClass.LoyaltyWallets.LoyaltyWalletCommand;
using static ConsistencyClass.LoyaltyWallets.LoyaltyWalletDecider;
using static ConsistencyClass.LoyaltyWallets.RedemptionWindows.RedemptionWindowCommand;
using static ConsistencyClass.LoyaltyWallets.RedemptionWindows.RedemptionWindowDecider;

namespace ConsistencyClass.Tests.LoyaltyWallets;

public class LoyaltyWalletStoreTests
{
    [Fact]
    public async Task RoundTripsAWalletByItsWalletNumber()
    {
        var store = LoyaltyWalletStoreFixture.CreateStore();
        var owner = MemberId.Random();
        var walletNumber = WalletNumber.Random();

        await store.SaveLoyaltyWallet(walletNumber, OpenLoyaltyWallet(
            new OpenLoyaltyWallet(walletNumber, owner, RedemptionLimit.Of(10), RedemptionCadence.Monthly),
            LoyaltyWallet.Initial()));

        var loaded = await store.GetLoyaltyWallet(walletNumber);

        loaded.ShouldBeOfType<LoyaltyWallet.Active>()
            .WalletNumber.ShouldBe(walletNumber);
    }

    public class RedemptionWindowStoreTests
    {
        private static readonly DateTime At = new(2026, 6, 23, 12, 0, 0, DateTimeKind.Utc);

        private readonly RedemptionWindowStore _store = new();

        [Fact]
        public async Task RoundTripsARedemptionWindow()
        {
            var owner = MemberId.Random();
            var walletNumber = WalletNumber.Random();

            await _store.SaveRedemptionWindow(walletNumber, 1, OpenRedemptionWindow(
                new OpenRedemptionWindow(walletNumber, owner, 1, LoyaltyPoints.Zero, RedemptionLimit.Of(5), [owner]),
                RedemptionWindow.Initial()));

            var loaded = await _store.GetRedemptionWindow(walletNumber, 1);

            loaded.ShouldBeOfType<RedemptionWindow.Open>()
                .WalletNumber.ShouldBe(walletNumber);
        }

        [Fact]
        public async Task ProjectsCurrentWindowActivityReportAndMonthlySummary()
        {
            var owner = MemberId.Random();
            var walletNumber = WalletNumber.Random();
            var opened = OpenRedemptionWindow(
                new OpenRedemptionWindow(walletNumber, owner, 1, LoyaltyPoints.Zero, RedemptionLimit.Of(5), [owner]),
                RedemptionWindow.Initial());
            await _store.SaveRedemptionWindow(walletNumber, 1, opened);

            var window = await _store.GetRedemptionWindow(walletNumber, 1);
            var earned = EarnLoyaltyPoints(new EarnLoyaltyPoints(walletNumber, LoyaltyPoints.Of(100), At), window);
            await _store.SaveRedemptionWindow(walletNumber, 1, [earned]);

            window = await _store.GetRedemptionWindow(walletNumber, 1);
            var redeemed = RedeemLoyaltyPoints(
                new RedeemLoyaltyPoints(walletNumber, owner, LoyaltyPoints.Of(40), At),
                window);
            await _store.SaveRedemptionWindow(walletNumber, 1, [redeemed]);

            window = await _store.GetRedemptionWindow(walletNumber, 1);
            var closed = CloseRedemptionWindow(new CloseRedemptionWindow(walletNumber, At), window);
            await _store.SaveRedemptionWindow(walletNumber, 1, closed);

            var current = await _store.CurrentWindowOf(walletNumber);
            current.ShouldNotBeNull();
            current.Open.ShouldBeFalse();

            var report = await _store.ActivityReports.Find(walletNumber.Value);
            report.ShouldNotBeNull();
            report.ClosedWindows.Count.ShouldBe(1);
            report.ClosedWindows[0].Earned.ShouldBe(100);
            report.ClosedWindows[0].Redeemed.ShouldBe(40);

            var summary = await _store.MonthlySummaries.Find($"{walletNumber.Value}:2026-06");
            summary.ShouldNotBeNull();
            summary.TotalEarned.ShouldBe(100);
            summary.TotalRedeemed.ShouldBe(40);
            summary.WindowsClosed.ShouldBe(1);
        }
    }
}
