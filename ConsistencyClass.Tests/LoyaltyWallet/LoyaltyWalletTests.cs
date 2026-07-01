using ConsistencyClass.LoyaltyWallets;
using ConsistencyClass.LoyaltyWallets.Access;
using ConsistencyClass.Membership;
using Ogooreck.BusinessLogic;
using static ConsistencyClass.LoyaltyWallets.LoyaltyWalletCommand;
using static ConsistencyClass.LoyaltyWallets.LoyaltyWalletDecider;

namespace ConsistencyClass.Tests.LoyaltyWallets;

public class LoyaltyWalletFixture
{
    public readonly WalletNumber WalletNumber = WalletNumber.Random();
    public readonly MemberId Owner = MemberId.Random();
    public readonly MemberId FamilyMember = MemberId.Random();
    public readonly DateTime At = new DateTime(2026, 6, 23, 12, 0, 0, DateTimeKind.Utc);
    public readonly DeciderSpecification<LoyaltyWalletCommand, LoyaltyWalletEvent, LoyaltyWallet> Spec =
        Specification.For<LoyaltyWalletCommand, LoyaltyWalletEvent, LoyaltyWallet>(
            (command, wallet) => Decide(command, wallet).ToArray(),
            Evolve,
            LoyaltyWallet.Initial);

    public static LoyaltyWallet Apply(LoyaltyWallet state, IReadOnlyList<LoyaltyWalletEvent> events) =>
        events.Aggregate(state, Evolve);

    public static LoyaltyWallet Apply(LoyaltyWallet state, LoyaltyWalletEvent @event) =>
        Evolve(state, @event);

    public LoyaltyWallet.Active OpenWallet() =>
        (LoyaltyWallet.Active)Apply(LoyaltyWallet.Initial(), OpenLoyaltyWallet(
            new OpenLoyaltyWallet(WalletNumber, Owner, RedemptionLimit.Of(5), RedemptionCadence.Weekly),
            LoyaltyWallet.Initial()));

    public LoyaltyWallet.Active WalletWithPoints(int points)
    {
        var wallet = OpenWallet();
        return (LoyaltyWallet.Active)Apply(wallet, EarnLoyaltyPoints(
            new EarnLoyaltyPoints(WalletNumber, LoyaltyPoints.Of(points), At),
            wallet));
    }

    public LoyaltyWallet.Deactivated Deactivated()
    {
        var wallet = OpenWallet();
        return (LoyaltyWallet.Deactivated)Apply(wallet, DeactivateWallet(wallet));
    }

    public LoyaltyWallet.Closed Closed()
    {
        var wallet = OpenWallet();
        return (LoyaltyWallet.Closed)Apply(wallet, CloseWallet(wallet));
    }
}

public class LoyaltyWalletTests
{
    public class Opening(LoyaltyWalletFixture fixture): IClassFixture<LoyaltyWalletFixture>
    {
        [Fact]
        public void OpensNotExistingWalletAndEmitsLoyaltyWalletOpened()
        {
            var expected = new LoyaltyWalletEvent.LoyaltyWalletOpened(
                fixture.WalletNumber,
                fixture.Owner,
                RedemptionCadence.Weekly,
                RedemptionLimit.Of(5),
                LoyaltyPoints.Zero,
                LoyaltyPoints.Zero);

            fixture.Spec.Given()
                .When(new OpenLoyaltyWallet(fixture.WalletNumber, fixture.Owner, RedemptionLimit.Of(5), RedemptionCadence.Weekly))
                .Then(expected);
        }

        [Fact]
        public void LeavesAlreadyActiveWalletUnchangedWithoutEvents()
        {
            var activeWallet = fixture.OpenWallet();

            var events = OpenLoyaltyWallet(
                new OpenLoyaltyWallet(fixture.WalletNumber, fixture.Owner, RedemptionLimit.Of(3), RedemptionCadence.Monthly),
                activeWallet);
            var newState = LoyaltyWalletFixture.Apply(activeWallet, events);

            newState.ShouldBeSameAs(activeWallet);
            events.ShouldBeEmpty();
        }

        [Fact]
        public void LeavesDeactivatedWalletUnchanged()
        {
            var deactivatedWallet = fixture.Deactivated();

            var events = OpenLoyaltyWallet(
                new OpenLoyaltyWallet(fixture.WalletNumber, fixture.Owner, RedemptionLimit.Of(5), RedemptionCadence.Weekly),
                deactivatedWallet);
            var newState = LoyaltyWalletFixture.Apply(deactivatedWallet, events);

            newState.ShouldBeSameAs(deactivatedWallet);
            events.ShouldBeEmpty();
        }

        [Fact]
        public void LeavesClosedWalletUnchanged()
        {
            var closedWallet = fixture.Closed();

            var events = OpenLoyaltyWallet(
                new OpenLoyaltyWallet(fixture.WalletNumber, fixture.Owner, RedemptionLimit.Of(5), RedemptionCadence.Weekly),
                closedWallet);
            var newState = LoyaltyWalletFixture.Apply(closedWallet, events);

            newState.ShouldBeSameAs(closedWallet);
            events.ShouldBeEmpty();
        }
    }

    public class EarningPoints(LoyaltyWalletFixture fixture): IClassFixture<LoyaltyWalletFixture>
    {
        [Fact]
        public void EarnsPointsOnActiveWalletAndEmitsLoyaltyPointsEarned()
        {
            var wallet = fixture.OpenWallet();
            var earned = EarnLoyaltyPoints(
                new EarnLoyaltyPoints(fixture.WalletNumber, LoyaltyPoints.Of(100), fixture.At),
                wallet);
            var newState = (LoyaltyWallet.Active)LoyaltyWalletFixture.Apply(wallet, earned);

            newState.ShouldBe(wallet with { PointsLimit = wallet.PointsLimit.Earn(LoyaltyPoints.Of(100)) });
            earned.ShouldBe(new LoyaltyWalletEvent.LoyaltyPointsEarned(
                fixture.WalletNumber, fixture.Owner, LoyaltyPoints.Of(100), fixture.At));
        }

        [Fact]
        public void CannotEarnOnNotExistingWallet()
        {
            Should.Throw<InvalidOperationException>(() =>
                    EarnLoyaltyPoints(
                        new EarnLoyaltyPoints(fixture.WalletNumber, LoyaltyPoints.Of(100), fixture.At),
                        LoyaltyWallet.Initial()))
                .Message.ShouldBe("Wallet doesn't exist");
        }

        [Fact]
        public void CannotEarnOnDeactivatedWallet()
        {
            Should.Throw<InvalidOperationException>(() =>
                    EarnLoyaltyPoints(
                        new EarnLoyaltyPoints(fixture.WalletNumber, LoyaltyPoints.Of(100), fixture.At),
                        fixture.Deactivated()))
                .Message.ShouldBe("Wallet is not active");
        }

        [Fact]
        public void CannotEarnOnClosedWallet()
        {
            Should.Throw<InvalidOperationException>(() =>
                    EarnLoyaltyPoints(
                        new EarnLoyaltyPoints(fixture.WalletNumber, LoyaltyPoints.Of(100), fixture.At),
                        fixture.Closed()))
                .Message.ShouldBe("Wallet is closed");
        }
    }

    public class RedeemingPoints(LoyaltyWalletFixture fixture): IClassFixture<LoyaltyWalletFixture>
    {
        [Fact]
        public void RedeemsPointsOnActiveWalletAndEmitsLoyaltyPointsRedeemed()
        {
            var wallet = fixture.WalletWithPoints(100);
            var redeemed = RedeemLoyaltyPoints(
                new RedeemLoyaltyPoints(fixture.WalletNumber, fixture.Owner, LoyaltyPoints.Of(40), fixture.At),
                wallet);
            var newState = (LoyaltyWallet.Active)LoyaltyWalletFixture.Apply(wallet, redeemed);

            newState.ShouldBe(wallet with { PointsLimit = wallet.PointsLimit.Redeem(LoyaltyPoints.Of(40)) });
            redeemed.ShouldBe(new LoyaltyWalletEvent.LoyaltyPointsRedeemed(
                fixture.WalletNumber, fixture.Owner, fixture.Owner,
                LoyaltyPoints.Of(40), LoyaltyPoints.Of(40), fixture.At));
        }

        [Fact]
        public void BurnsThePolicyAmountWhileRecordingTheRedeemedAmount()
        {
            var wallet = fixture.WalletWithPoints(100);
            var redeemed = RedeemLoyaltyPoints(
                new RedeemLoyaltyPoints(fixture.WalletNumber, fixture.Owner, LoyaltyPoints.Of(100), fixture.At, LoyaltyPoints.Of(95)),
                wallet);
            var newState = (LoyaltyWallet.Active)LoyaltyWalletFixture.Apply(wallet, redeemed);

            newState.ShouldBe(wallet with { PointsLimit = wallet.PointsLimit.Redeem(LoyaltyPoints.Of(95)) });
            redeemed.ShouldBe(new LoyaltyWalletEvent.LoyaltyPointsRedeemed(
                fixture.WalletNumber, fixture.Owner, fixture.Owner,
                LoyaltyPoints.Of(100), LoyaltyPoints.Of(95), fixture.At));
        }

        [Fact]
        public void CannotRedeemMoreThanAvailable()
        {
            Should.Throw<InvalidOperationException>(() =>
                    RedeemLoyaltyPoints(
                        new RedeemLoyaltyPoints(fixture.WalletNumber, fixture.Owner, LoyaltyPoints.Of(50), fixture.At),
                        fixture.WalletWithPoints(20)))
                .Message.ShouldBe("Not enough points to redeem");
        }

        [Fact]
        public void CannotRedeemOnNotExistingWallet()
        {
            Should.Throw<InvalidOperationException>(() =>
                    RedeemLoyaltyPoints(
                        new RedeemLoyaltyPoints(fixture.WalletNumber, fixture.Owner, LoyaltyPoints.Of(40), fixture.At),
                        LoyaltyWallet.Initial()))
                .Message.ShouldBe("Wallet doesn't exist");
        }

        [Fact]
        public void CannotRedeemOnDeactivatedWallet()
        {
            Should.Throw<InvalidOperationException>(() =>
                    RedeemLoyaltyPoints(
                        new RedeemLoyaltyPoints(fixture.WalletNumber, fixture.Owner, LoyaltyPoints.Of(40), fixture.At),
                        fixture.Deactivated()))
                .Message.ShouldBe("Wallet is not active");
        }

        [Fact]
        public void CannotRedeemOnClosedWallet()
        {
            Should.Throw<InvalidOperationException>(() =>
                    RedeemLoyaltyPoints(
                        new RedeemLoyaltyPoints(fixture.WalletNumber, fixture.Owner, LoyaltyPoints.Of(40), fixture.At),
                        fixture.Closed()))
                .Message.ShouldBe("Wallet is closed");
        }
    }

    public class SettingRedemptionCadence(LoyaltyWalletFixture fixture): IClassFixture<LoyaltyWalletFixture>
    {
        [Fact]
        public void ChangesCadenceOnActiveWalletAndEmitsRedemptionCadenceSet()
        {
            var opened = fixture.OpenWallet();

            var cadenceSet = SetRedemptionCadence(
                new SetRedemptionCadence(fixture.WalletNumber, RedemptionCadence.Monthly),
                opened);
            var newState = (LoyaltyWallet.Active)LoyaltyWalletFixture.Apply(opened, cadenceSet);

            newState.ShouldBe(opened with { Cadence = RedemptionCadence.Monthly });
            cadenceSet.ShouldBe(new LoyaltyWalletEvent.RedemptionCadenceSet(
                fixture.WalletNumber, fixture.Owner, RedemptionCadence.Monthly));
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
        public void ResetsRedemptionCountKeepingBalanceAndEmitsRedemptionWindowReset()
        {
            var wallet = fixture.WalletWithPoints(100);
            var walletAfterRedeem = (LoyaltyWallet.Active)LoyaltyWalletFixture.Apply(wallet, RedeemLoyaltyPoints(
                new RedeemLoyaltyPoints(fixture.WalletNumber, fixture.Owner, LoyaltyPoints.Of(10), fixture.At),
                wallet));

            var reset = ResetRedemptionWindow(
                new ResetRedemptionWindow(fixture.WalletNumber, fixture.At),
                walletAfterRedeem);
            var newState = (LoyaltyWallet.Active)LoyaltyWalletFixture.Apply(walletAfterRedeem, reset);

            newState.ShouldBe(walletAfterRedeem with
            {
                PointsLimit = walletAfterRedeem.PointsLimit.ResetRedemptionCount()
            });
            reset.ShouldBe(new LoyaltyWalletEvent.RedemptionWindowReset(
                fixture.WalletNumber, fixture.Owner, fixture.At));
        }

        [Fact]
        public void CannotResetWindowIfWalletNotActive()
        {
            Should.Throw<InvalidOperationException>(() =>
                    ResetRedemptionWindow(
                        new ResetRedemptionWindow(fixture.WalletNumber, fixture.At),
                        fixture.Deactivated()))
                .Message.ShouldBe("Wallet is not active");
        }
    }

    public class Deactivating(LoyaltyWalletFixture fixture): IClassFixture<LoyaltyWalletFixture>
    {
        [Fact]
        public void DeactivatesActiveWalletKeepingDataAndEmitsWalletDeactivated()
        {
            var wallet = fixture.WalletWithPoints(100);
            var walletAfterRedeem = (LoyaltyWallet.Active)LoyaltyWalletFixture.Apply(wallet, RedeemLoyaltyPoints(
                new RedeemLoyaltyPoints(fixture.WalletNumber, fixture.Owner, LoyaltyPoints.Of(10), fixture.At),
                wallet));

            var events = DeactivateWallet(walletAfterRedeem);
            var newState = LoyaltyWalletFixture.Apply(walletAfterRedeem, events);

            newState.ShouldBe(new LoyaltyWallet.Deactivated(
                walletAfterRedeem.WalletNumber,
                walletAfterRedeem.OwnerId,
                walletAfterRedeem.PointsLimit,
                walletAfterRedeem.Cadence,
                walletAfterRedeem.Access));
            events.ShouldHaveSingleItem().ShouldBe(new LoyaltyWalletEvent.WalletDeactivated(
                fixture.WalletNumber, fixture.Owner));
        }

        [Fact]
        public void LeavesAlreadyDeactivatedWalletUnchangedWithoutEvents()
        {
            var deactivated = fixture.Deactivated();

            var events = DeactivateWallet(deactivated);
            var newState = LoyaltyWalletFixture.Apply(deactivated, events);

            newState.ShouldBeSameAs(deactivated);
            events.ShouldBeEmpty();
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
                    DeactivateWallet(fixture.Closed()))
                .Message.ShouldBe("Wallet is closed");
        }
    }

    public class Closing(LoyaltyWalletFixture fixture): IClassFixture<LoyaltyWalletFixture>
    {
        [Fact]
        public void ClosesActiveWalletAndEmitsWalletClosed()
        {
            var wallet = fixture.OpenWallet();
            var events = CloseWallet(wallet);
            var newState = LoyaltyWalletFixture.Apply(wallet, events);

            newState.ShouldBe(new LoyaltyWallet.Closed(fixture.WalletNumber));
            events.ShouldHaveSingleItem().ShouldBe(new LoyaltyWalletEvent.WalletClosed(fixture.WalletNumber));
        }

        [Fact]
        public void ClosesDeactivatedWallet()
        {
            var deactivated = fixture.Deactivated();
            LoyaltyWalletFixture.Apply(deactivated, CloseWallet(deactivated))
                .ShouldBe(new LoyaltyWallet.Closed(fixture.WalletNumber));
        }

        [Fact]
        public void LeavesAlreadyClosedWalletUnchangedWithoutEvents()
        {
            var closed = fixture.Closed();

            var events = CloseWallet(closed);
            var newState = LoyaltyWalletFixture.Apply(closed, events);

            newState.ShouldBeSameAs(closed);
            events.ShouldBeEmpty();
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
            var events = Decide(
                new OpenLoyaltyWallet(fixture.WalletNumber, fixture.Owner, RedemptionLimit.Of(5), RedemptionCadence.Weekly),
                LoyaltyWallet.Initial());
            var newState = LoyaltyWalletFixture.Apply(LoyaltyWallet.Initial(), events);

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
            var events = Decide(
                new EarnLoyaltyPoints(fixture.WalletNumber, LoyaltyPoints.Of(100), fixture.At),
                wallet);
            var newState = LoyaltyWalletFixture.Apply(wallet, events);

            newState.ShouldBe(wallet with { PointsLimit = wallet.PointsLimit.Earn(LoyaltyPoints.Of(100)) });
        }

        [Fact]
        public void RoutesRedeemLoyaltyPoints()
        {
            var wallet = fixture.WalletWithPoints(100);
            var events = Decide(
                new RedeemLoyaltyPoints(fixture.WalletNumber, fixture.Owner, LoyaltyPoints.Of(40), fixture.At),
                wallet);
            var newState = LoyaltyWalletFixture.Apply(wallet, events);

            newState.ShouldBe(wallet with { PointsLimit = wallet.PointsLimit.Redeem(LoyaltyPoints.Of(40)) });
        }

        [Fact]
        public void RoutesSetRedemptionCadence()
        {
            var opened = fixture.OpenWallet();

            var events = Decide(
                new SetRedemptionCadence(fixture.WalletNumber, RedemptionCadence.Monthly),
                opened);
            var newState = LoyaltyWalletFixture.Apply(opened, events);

            newState.ShouldBe(opened with { Cadence = RedemptionCadence.Monthly });
        }

        [Fact]
        public void RoutesResetRedemptionWindow()
        {
            var wallet = fixture.WalletWithPoints(100);
            var walletAfterRedeem = (LoyaltyWallet.Active)LoyaltyWalletFixture.Apply(wallet, RedeemLoyaltyPoints(
                new RedeemLoyaltyPoints(fixture.WalletNumber, fixture.Owner, LoyaltyPoints.Of(10), fixture.At),
                wallet));

            var events = Decide(
                new ResetRedemptionWindow(fixture.WalletNumber, fixture.At),
                walletAfterRedeem);
            var newState = LoyaltyWalletFixture.Apply(walletAfterRedeem, events);

            newState.ShouldBe(walletAfterRedeem with
            {
                PointsLimit = walletAfterRedeem.PointsLimit.ResetRedemptionCount()
            });
        }

        [Fact]
        public void RoutesDeactivateWallet()
        {
            var wallet = fixture.OpenWallet();
            var events = Decide(new DeactivateWallet(fixture.WalletNumber), wallet);
            var newState = LoyaltyWalletFixture.Apply(wallet, events);
            newState.ShouldBe(new LoyaltyWallet.Deactivated(
                wallet.WalletNumber,
                wallet.OwnerId,
                wallet.PointsLimit,
                wallet.Cadence,
                wallet.Access));
        }

        [Fact]
        public void RoutesCloseWallet()
        {
            var wallet = fixture.OpenWallet();
            var events = Decide(new CloseWallet(fixture.WalletNumber), wallet);
            var newState = LoyaltyWalletFixture.Apply(wallet, events);
            newState.ShouldBe(new LoyaltyWallet.Closed(fixture.WalletNumber));
        }

        [Fact]
        public void RoutesGrantWalletAccess()
        {
            var wallet = fixture.OpenWallet();
            var events = Decide(
                new GrantWalletAccess(fixture.WalletNumber, fixture.FamilyMember),
                wallet);
            var newState = LoyaltyWalletFixture.Apply(wallet, events);

            newState.ShouldBe(wallet with { Access = wallet.Access.Add(fixture.FamilyMember) });
        }

        [Fact]
        public void RoutesRevokeWalletAccess()
        {
            var wallet = fixture.OpenWallet();
            var shared = (LoyaltyWallet.Active)LoyaltyWalletFixture.Apply(wallet, GrantWalletAccess(
                new GrantWalletAccess(fixture.WalletNumber, fixture.FamilyMember),
                wallet));

            var events = Decide(
                new RevokeWalletAccess(fixture.WalletNumber, fixture.FamilyMember),
                shared);
            var newState = LoyaltyWalletFixture.Apply(shared, events);

            newState.ShouldBe(shared with { Access = shared.Access.Revoke(fixture.FamilyMember) });
        }
    }

    public class GrantingAccess(LoyaltyWalletFixture fixture): IClassFixture<LoyaltyWalletFixture>
    {
        [Fact]
        public void GrantsAccessToFamilyMemberAndEmitsWalletAccessGranted()
        {
            var wallet = fixture.OpenWallet();
            var granted = GrantWalletAccess(
                new GrantWalletAccess(fixture.WalletNumber, fixture.FamilyMember),
                wallet);
            var newState = (LoyaltyWallet.Active)LoyaltyWalletFixture.Apply(wallet, granted);

            newState.ShouldBe(wallet with { Access = wallet.Access.Add(fixture.FamilyMember) });
            granted.ShouldBe(new LoyaltyWalletEvent.WalletAccessGranted(
                fixture.WalletNumber, fixture.Owner, fixture.FamilyMember));
        }

        [Fact]
        public void RevokesAccessFromFamilyMemberAndEmitsWalletAccessRevoked()
        {
            var wallet = fixture.OpenWallet();
            var shared = (LoyaltyWallet.Active)LoyaltyWalletFixture.Apply(wallet, GrantWalletAccess(
                new GrantWalletAccess(fixture.WalletNumber, fixture.FamilyMember),
                wallet));

            var revoked = RevokeWalletAccess(
                new RevokeWalletAccess(fixture.WalletNumber, fixture.FamilyMember),
                shared);
            var newState = (LoyaltyWallet.Active)LoyaltyWalletFixture.Apply(shared, revoked);

            newState.ShouldBe(shared with { Access = shared.Access.Revoke(fixture.FamilyMember) });
            revoked.ShouldBe(new LoyaltyWalletEvent.WalletAccessRevoked(
                fixture.WalletNumber, fixture.Owner, fixture.FamilyMember));
        }

        [Fact]
        public void OwnerCanRedeemFromSharedBalance()
        {
            var wallet = fixture.WalletWithPoints(100);
            var redeemed = RedeemLoyaltyPoints(
                new RedeemLoyaltyPoints(fixture.WalletNumber, fixture.Owner, LoyaltyPoints.Of(40), fixture.At),
                wallet);
            var newState = (LoyaltyWallet.Active)LoyaltyWalletFixture.Apply(wallet, redeemed);

            newState.ShouldBe(wallet with { PointsLimit = wallet.PointsLimit.Redeem(LoyaltyPoints.Of(40)) });
        }

        [Fact]
        public void GrantedFamilyMemberCanRedeemFromSharedBalance()
        {
            // given
            var wallet = fixture.WalletWithPoints(100);
            var shared = (LoyaltyWallet.Active)LoyaltyWalletFixture.Apply(wallet, GrantWalletAccess(
                new GrantWalletAccess(fixture.WalletNumber, fixture.FamilyMember),
                wallet));

            // when
            var redeemed = RedeemLoyaltyPoints(
                new RedeemLoyaltyPoints(fixture.WalletNumber, fixture.FamilyMember, LoyaltyPoints.Of(40), fixture.At),
                shared);
            var newState = (LoyaltyWallet.Active)LoyaltyWalletFixture.Apply(shared, redeemed);

            // then
            newState.ShouldBe(shared with { PointsLimit = shared.PointsLimit.Redeem(LoyaltyPoints.Of(40)) });
        }

        [Fact]
        public void CannotRedeemWithoutAccess()
        {
            Should.Throw<InvalidOperationException>(() =>
                    RedeemLoyaltyPoints(
                        new RedeemLoyaltyPoints(fixture.WalletNumber, fixture.FamilyMember, LoyaltyPoints.Of(40), fixture.At),
                        fixture.WalletWithPoints(100)))
                .Message.ShouldBe("Not authorized to redeem");
        }

        [Fact]
        public void CannotRedeemAfterAccessIsRevoked()
        {
            // given
            var wallet = fixture.WalletWithPoints(100);
            var shared = (LoyaltyWallet.Active)LoyaltyWalletFixture.Apply(wallet, GrantWalletAccess(
                new GrantWalletAccess(fixture.WalletNumber, fixture.FamilyMember),
                wallet));
            // and
            var revoked = (LoyaltyWallet.Active)LoyaltyWalletFixture.Apply(shared, RevokeWalletAccess(
                new RevokeWalletAccess(fixture.WalletNumber, fixture.FamilyMember),
                shared));

            Should.Throw<InvalidOperationException>(() =>
                    RedeemLoyaltyPoints(
                        new RedeemLoyaltyPoints(fixture.WalletNumber, fixture.FamilyMember, LoyaltyPoints.Of(40), fixture.At),
                        revoked))
                .Message.ShouldBe("Not authorized to redeem");
        }

        [Fact]
        public void CannotGrantAccessIfWalletNotActive()
        {
            var deactivatedWallet = fixture.Deactivated();

            Should.Throw<InvalidOperationException>(() =>
                    GrantWalletAccess(
                        new GrantWalletAccess(fixture.WalletNumber, fixture.FamilyMember),
                        deactivatedWallet))
                .Message.ShouldBe("Wallet is not active");
        }

        [Fact]
        public void CannotRevokeAccessIfWalletNotActive()
        {
            var deactivatedWallet = fixture.Deactivated();

            Should.Throw<InvalidOperationException>(() =>
                    RevokeWalletAccess(
                        new RevokeWalletAccess(fixture.WalletNumber, fixture.FamilyMember),
                        deactivatedWallet))
                .Message.ShouldBe("Wallet is not active");
        }
    }
}
