using ConsistencyClass.LoyaltyWallets;
using ConsistencyClass.LoyaltyWallets.Access;
using ConsistencyClass.Membership;
using static ConsistencyClass.LoyaltyWallets.LoyaltyWalletCommand;
using static ConsistencyClass.LoyaltyWallets.LoyaltyWalletDecider;

namespace ConsistencyClass.Tests.LoyaltyWallets;

public class LoyaltyWalletFixture
{
    public readonly WalletNumber WalletNumber = WalletNumber.Random();
    public readonly MemberId Owner = MemberId.Random();
    public readonly MemberId FamilyMember = MemberId.Random();

    public LoyaltyWallet.Active OpenWallet() =>
        (LoyaltyWallet.Active)OpenLoyaltyWallet(
            new OpenLoyaltyWallet(WalletNumber, Owner, RedemptionLimit.Of(5), RedemptionCadence.Weekly),
            LoyaltyWallet.Initial());

    public LoyaltyWallet.Active WalletWithPoints(int points) =>
        (LoyaltyWallet.Active)EarnLoyaltyPoints(
            new EarnLoyaltyPoints(WalletNumber, LoyaltyPoints.Of(points)),
            OpenWallet());
}

public class LoyaltyWalletTests
{
    public class Opening(LoyaltyWalletFixture fixture): IClassFixture<LoyaltyWalletFixture>
    {
        [Fact]
        public void OpensNotExistingWallet()
        {
            var newState = OpenLoyaltyWallet(
                new OpenLoyaltyWallet(fixture.WalletNumber, fixture.Owner, RedemptionLimit.Of(5), RedemptionCadence.Weekly),
                LoyaltyWallet.Initial());

            newState.ShouldBe(new LoyaltyWallet.Active(
                fixture.WalletNumber,
                fixture.Owner,
                LoyaltyPointsLimit.Initial(RedemptionLimit.Of(5)),
                RedemptionCadence.Weekly,
                WalletAccess.Of(fixture.Owner)));
        }

        [Fact]
        public void LeavesAlreadyActiveWalletUnchanged()
        {
            var activeWallet = fixture.OpenWallet();

            var newState = OpenLoyaltyWallet(
                new OpenLoyaltyWallet(fixture.WalletNumber, fixture.Owner, RedemptionLimit.Of(3), RedemptionCadence.Monthly),
                activeWallet);

            newState.ShouldBeSameAs(activeWallet);
        }

        [Fact]
        public void LeavesDeactivatedWalletUnchanged()
        {
            var deactivatedWallet = DeactivateWallet(fixture.OpenWallet());

            var newState = OpenLoyaltyWallet(
                new OpenLoyaltyWallet(fixture.WalletNumber, fixture.Owner, RedemptionLimit.Of(5), RedemptionCadence.Weekly),
                deactivatedWallet);

            newState.ShouldBeSameAs(deactivatedWallet);
        }

        [Fact]
        public void LeavesClosedWalletUnchanged()
        {
            var closedWallet = CloseWallet(fixture.OpenWallet());

            var newState = OpenLoyaltyWallet(
                new OpenLoyaltyWallet(fixture.WalletNumber, fixture.Owner, RedemptionLimit.Of(5), RedemptionCadence.Weekly),
                closedWallet);

            newState.ShouldBeSameAs(closedWallet);
        }
    }

    public class EarningPoints(LoyaltyWalletFixture fixture): IClassFixture<LoyaltyWalletFixture>
    {
        [Fact]
        public void EarnsPointsOnActiveWallet()
        {
            var wallet = fixture.OpenWallet();

            var newState = EarnLoyaltyPoints(
                new EarnLoyaltyPoints(fixture.WalletNumber, LoyaltyPoints.Of(100)),
                wallet);

            newState.ShouldBe(wallet with { PointsLimit = wallet.PointsLimit.Earn(LoyaltyPoints.Of(100)) });
        }

        [Fact]
        public void CannotEarnOnNotExistingWallet()
        {
            Should.Throw<InvalidOperationException>(() =>
                    EarnLoyaltyPoints(
                        new EarnLoyaltyPoints(fixture.WalletNumber, LoyaltyPoints.Of(100)),
                        LoyaltyWallet.Initial()))
                .Message.ShouldBe("Wallet doesn't exist");
        }

        [Fact]
        public void CannotEarnOnDeactivatedWallet()
        {
            Should.Throw<InvalidOperationException>(() =>
                    EarnLoyaltyPoints(
                        new EarnLoyaltyPoints(fixture.WalletNumber, LoyaltyPoints.Of(100)),
                        DeactivateWallet(fixture.OpenWallet())))
                .Message.ShouldBe("Wallet is not active");
        }

        [Fact]
        public void CannotEarnOnClosedWallet()
        {
            Should.Throw<InvalidOperationException>(() =>
                    EarnLoyaltyPoints(
                        new EarnLoyaltyPoints(fixture.WalletNumber, LoyaltyPoints.Of(100)),
                        CloseWallet(fixture.OpenWallet())))
                .Message.ShouldBe("Wallet is closed");
        }
    }

    public class RedeemingPoints(LoyaltyWalletFixture fixture): IClassFixture<LoyaltyWalletFixture>
    {
        [Fact]
        public void RedeemsPointsOnActiveWallet()
        {
            var wallet = fixture.WalletWithPoints(100);

            var newState = RedeemLoyaltyPoints(
                new RedeemLoyaltyPoints(fixture.WalletNumber, fixture.Owner, LoyaltyPoints.Of(40)),
                wallet);

            newState.ShouldBe(wallet with { PointsLimit = wallet.PointsLimit.Redeem(LoyaltyPoints.Of(40)) });
        }

        [Fact]
        public void CannotRedeemMoreThanAvailable()
        {
            Should.Throw<InvalidOperationException>(() =>
                    RedeemLoyaltyPoints(
                        new RedeemLoyaltyPoints(fixture.WalletNumber, fixture.Owner, LoyaltyPoints.Of(50)),
                        fixture.WalletWithPoints(20)))
                .Message.ShouldBe("Not enough points to redeem");
        }

        [Fact]
        public void CannotRedeemOnNotExistingWallet()
        {
            Should.Throw<InvalidOperationException>(() =>
                    RedeemLoyaltyPoints(
                        new RedeemLoyaltyPoints(fixture.WalletNumber, fixture.Owner, LoyaltyPoints.Of(40)),
                        LoyaltyWallet.Initial()))
                .Message.ShouldBe("Wallet doesn't exist");
        }

        [Fact]
        public void CannotRedeemOnDeactivatedWallet()
        {
            Should.Throw<InvalidOperationException>(() =>
                    RedeemLoyaltyPoints(
                        new RedeemLoyaltyPoints(fixture.WalletNumber, fixture.Owner, LoyaltyPoints.Of(40)),
                        DeactivateWallet(fixture.OpenWallet())))
                .Message.ShouldBe("Wallet is not active");
        }

        [Fact]
        public void CannotRedeemOnClosedWallet()
        {
            Should.Throw<InvalidOperationException>(() =>
                    RedeemLoyaltyPoints(
                        new RedeemLoyaltyPoints(fixture.WalletNumber, fixture.Owner, LoyaltyPoints.Of(40)),
                        CloseWallet(fixture.OpenWallet())))
                .Message.ShouldBe("Wallet is closed");
        }
    }

    public class SettingRedemptionCadence(LoyaltyWalletFixture fixture): IClassFixture<LoyaltyWalletFixture>
    {
        [Fact]
        public void ChangesCadenceOnActiveWallet()
        {
            var opened = fixture.OpenWallet();

            var newState = SetRedemptionCadence(
                new SetRedemptionCadence(fixture.WalletNumber, RedemptionCadence.Monthly),
                opened);

            newState.ShouldBe(opened with { Cadence = RedemptionCadence.Monthly });
        }

        [Fact]
        public void CannotSetCadenceOnNotExistingWallet()
        {
            Should.Throw<InvalidOperationException>(() =>
                    SetRedemptionCadence(
                        new SetRedemptionCadence(fixture.WalletNumber, RedemptionCadence.Monthly),
                        LoyaltyWallet.Initial()))
                .Message.ShouldBe("Wallet doesn't exist");
        }
    }

    public class ResettingRedemptionWindow(LoyaltyWalletFixture fixture): IClassFixture<LoyaltyWalletFixture>
    {
        [Fact]
        public void ResetsRedemptionCountKeepingBalance()
        {
            var walletAfterRedeem = RedeemLoyaltyPoints(
                new RedeemLoyaltyPoints(fixture.WalletNumber, fixture.Owner, LoyaltyPoints.Of(10)),
                fixture.WalletWithPoints(100));

            var newState = ResetRedemptionWindow(walletAfterRedeem);

            newState.ShouldBe(walletAfterRedeem with
            {
                PointsLimit = walletAfterRedeem.PointsLimit.ResetRedemptionCount()
            });
        }

        [Fact]
        public void CannotResetWindowIfWalletNotActive()
        {
            Should.Throw<InvalidOperationException>(() =>
                    ResetRedemptionWindow(DeactivateWallet(fixture.OpenWallet())))
                .Message.ShouldBe("Wallet is not active");
        }
    }

    public class Deactivating(LoyaltyWalletFixture fixture): IClassFixture<LoyaltyWalletFixture>
    {
        [Fact]
        public void DeactivatesActiveWalletKeepingData()
        {
            var walletAfterRedeem = RedeemLoyaltyPoints(
                new RedeemLoyaltyPoints(fixture.WalletNumber, fixture.Owner, LoyaltyPoints.Of(10)),
                fixture.WalletWithPoints(100));

            var newState = DeactivateWallet(walletAfterRedeem);

            newState.ShouldBe(new LoyaltyWallet.Deactivated(
                walletAfterRedeem.WalletNumber,
                walletAfterRedeem.OwnerId,
                walletAfterRedeem.PointsLimit,
                walletAfterRedeem.Cadence,
                walletAfterRedeem.Access));
        }

        [Fact]
        public void LeavesAlreadyDeactivatedWalletUnchanged()
        {
            var deactivated = DeactivateWallet(fixture.OpenWallet());

            var newState = DeactivateWallet(deactivated);

            newState.ShouldBeSameAs(deactivated);
        }

        [Fact]
        public void CannotDeactivateNotExistingWallet()
        {
            Should.Throw<InvalidOperationException>(() =>
                    DeactivateWallet(LoyaltyWallet.Initial()))
                .Message.ShouldBe("Wallet doesn't exist");
        }

        [Fact]
        public void CannotDeactivateClosedWallet()
        {
            Should.Throw<InvalidOperationException>(() =>
                    DeactivateWallet(CloseWallet(fixture.OpenWallet())))
                .Message.ShouldBe("Wallet is closed");
        }
    }

    public class Closing(LoyaltyWalletFixture fixture): IClassFixture<LoyaltyWalletFixture>
    {
        [Fact]
        public void ClosesActiveWallet()
        {
            CloseWallet(fixture.OpenWallet()).ShouldBeOfType<LoyaltyWallet.Closed>();
        }

        [Fact]
        public void ClosesDeactivatedWallet()
        {
            CloseWallet(DeactivateWallet(fixture.OpenWallet())).ShouldBeOfType<LoyaltyWallet.Closed>();
        }

        [Fact]
        public void LeavesAlreadyClosedWalletUnchanged()
        {
            var closed = CloseWallet(fixture.OpenWallet());

            CloseWallet(closed).ShouldBeSameAs(closed);
        }

        [Fact]
        public void CannotCloseNotExistingWallet()
        {
            Should.Throw<InvalidOperationException>(() =>
                    CloseWallet(LoyaltyWallet.Initial()))
                .Message.ShouldBe("Wallet doesn't exist");
        }
    }

    public class DecideRouting(LoyaltyWalletFixture fixture): IClassFixture<LoyaltyWalletFixture>
    {
        [Fact]
        public void RoutesOpenLoyaltyWallet()
        {
            var newState = Decide(
                new OpenLoyaltyWallet(fixture.WalletNumber, fixture.Owner, RedemptionLimit.Of(5), RedemptionCadence.Weekly),
                LoyaltyWallet.Initial());

            newState.ShouldBe(new LoyaltyWallet.Active(
                fixture.WalletNumber,
                fixture.Owner,
                LoyaltyPointsLimit.Initial(RedemptionLimit.Of(5)),
                RedemptionCadence.Weekly,
                WalletAccess.Of(fixture.Owner)));
        }

        [Fact]
        public void RoutesEarnLoyaltyPoints()
        {
            var wallet = fixture.OpenWallet();

            var newState = Decide(
                new EarnLoyaltyPoints(fixture.WalletNumber, LoyaltyPoints.Of(100)),
                wallet);

            newState.ShouldBe(wallet with { PointsLimit = wallet.PointsLimit.Earn(LoyaltyPoints.Of(100)) });
        }

        [Fact]
        public void RoutesRedeemLoyaltyPoints()
        {
            var wallet = fixture.WalletWithPoints(100);

            var newState = Decide(
                new RedeemLoyaltyPoints(fixture.WalletNumber, fixture.Owner, LoyaltyPoints.Of(40)),
                wallet);

            newState.ShouldBe(wallet with { PointsLimit = wallet.PointsLimit.Redeem(LoyaltyPoints.Of(40)) });
        }

        [Fact]
        public void RoutesSetRedemptionCadence()
        {
            var opened = fixture.OpenWallet();

            var newState = Decide(
                new SetRedemptionCadence(fixture.WalletNumber, RedemptionCadence.Monthly),
                opened);

            newState.ShouldBe(opened with { Cadence = RedemptionCadence.Monthly });
        }

        [Fact]
        public void RoutesResetRedemptionWindow()
        {
            var walletAfterRedeem = RedeemLoyaltyPoints(
                new RedeemLoyaltyPoints(fixture.WalletNumber, fixture.Owner, LoyaltyPoints.Of(10)),
                fixture.WalletWithPoints(100));

            var newState = Decide(new ResetRedemptionWindow(fixture.WalletNumber), walletAfterRedeem);

            newState.ShouldBe(walletAfterRedeem with
            {
                PointsLimit = walletAfterRedeem.PointsLimit.ResetRedemptionCount()
            });
        }

        [Fact]
        public void RoutesDeactivateWallet()
        {
            var wallet = fixture.OpenWallet();

            Decide(new DeactivateWallet(fixture.WalletNumber), wallet)
                .ShouldBe(new LoyaltyWallet.Deactivated(
                    wallet.WalletNumber,
                    wallet.OwnerId,
                    wallet.PointsLimit,
                    wallet.Cadence,
                    wallet.Access));
        }

        [Fact]
        public void RoutesCloseWallet()
        {
            Decide(new CloseWallet(fixture.WalletNumber), fixture.OpenWallet())
                .ShouldBeOfType<LoyaltyWallet.Closed>();
        }

        [Fact]
        public void RoutesGrantWalletAccess()
        {
            var wallet = fixture.OpenWallet();

            var newState = Decide(
                new GrantWalletAccess(fixture.WalletNumber, fixture.FamilyMember),
                wallet);

            newState.ShouldBe(wallet with { Access = wallet.Access.Add(fixture.FamilyMember) });
        }

        [Fact]
        public void RoutesRevokeWalletAccess()
        {
            var wallet = fixture.OpenWallet();
            var shared = GrantWalletAccess(
                new GrantWalletAccess(fixture.WalletNumber, fixture.FamilyMember),
                wallet);

            var newState = Decide(
                new RevokeWalletAccess(fixture.WalletNumber, fixture.FamilyMember),
                shared);

            newState.ShouldBe(shared with { Access = shared.Access.Revoke(fixture.FamilyMember) });
        }
    }

    public class GrantingAccess(LoyaltyWalletFixture fixture): IClassFixture<LoyaltyWalletFixture>
    {
        [Fact]
        public void OwnerCanRedeemFromSharedBalance()
        {
            var wallet = fixture.WalletWithPoints(100);

            var newState = RedeemLoyaltyPoints(
                new RedeemLoyaltyPoints(fixture.WalletNumber, fixture.Owner, LoyaltyPoints.Of(40)),
                wallet);

            newState.ShouldBe(wallet with { PointsLimit = wallet.PointsLimit.Redeem(LoyaltyPoints.Of(40)) });
        }

        [Fact]
        public void GrantedFamilyMemberCanRedeemFromSharedBalance()
        {
            // given
            var wallet = fixture.WalletWithPoints(100);
            var shared = GrantWalletAccess(
                new GrantWalletAccess(fixture.WalletNumber, fixture.FamilyMember),
                wallet);

            // when
            var newState = RedeemLoyaltyPoints(
                new RedeemLoyaltyPoints(fixture.WalletNumber, fixture.FamilyMember, LoyaltyPoints.Of(40)),
                shared);

            // then
            newState.ShouldBe(shared with { PointsLimit = shared.PointsLimit.Redeem(LoyaltyPoints.Of(40)) });
        }

        [Fact]
        public void CannotRedeemWithoutAccess()
        {
            Should.Throw<InvalidOperationException>(() =>
                    RedeemLoyaltyPoints(
                        new RedeemLoyaltyPoints(fixture.WalletNumber, fixture.FamilyMember, LoyaltyPoints.Of(40)),
                        fixture.WalletWithPoints(100)))
                .Message.ShouldBe("Not authorized to redeem");
        }

        [Fact]
        public void CannotRedeemAfterAccessIsRevoked()
        {
            // given
            var shared = GrantWalletAccess(
                new GrantWalletAccess(fixture.WalletNumber, fixture.FamilyMember),
                fixture.WalletWithPoints(100));
            // and
            var revoked = RevokeWalletAccess(
                new RevokeWalletAccess(fixture.WalletNumber, fixture.FamilyMember),
                shared);

            Should.Throw<InvalidOperationException>(() =>
                    RedeemLoyaltyPoints(
                        new RedeemLoyaltyPoints(fixture.WalletNumber, fixture.FamilyMember, LoyaltyPoints.Of(40)),
                        revoked))
                .Message.ShouldBe("Not authorized to redeem");
        }

        [Fact]
        public void CannotGrantAccessIfWalletNotActive()
        {
            var deactivatedWallet = DeactivateWallet(fixture.OpenWallet());

            Should.Throw<InvalidOperationException>(() =>
                    GrantWalletAccess(
                        new GrantWalletAccess(fixture.WalletNumber, fixture.FamilyMember),
                        deactivatedWallet))
                .Message.ShouldBe("Wallet is not active");
        }

        [Fact]
        public void CannotRevokeAccessIfWalletNotActive()
        {
            var deactivatedWallet = DeactivateWallet(fixture.OpenWallet());

            Should.Throw<InvalidOperationException>(() =>
                    RevokeWalletAccess(
                        new RevokeWalletAccess(fixture.WalletNumber, fixture.FamilyMember),
                        deactivatedWallet))
                .Message.ShouldBe("Wallet is not active");
        }
    }
}
