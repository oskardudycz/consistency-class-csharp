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
    public readonly DeciderSpecification<LoyaltyWalletCommand, LoyaltyWalletEvent, LoyaltyWallet> Spec =
        Specification.For<LoyaltyWalletCommand, LoyaltyWalletEvent, LoyaltyWallet>(
            (command, wallet) => Decide(command, wallet).ToArray(),
            Evolve,
            LoyaltyWallet.Initial);

    public static LoyaltyWallet Apply(LoyaltyWallet state, IReadOnlyList<LoyaltyWalletEvent> events) =>
        events.Aggregate(state, Evolve);

    public LoyaltyWallet.Active OpenWallet() =>
        (LoyaltyWallet.Active)Apply(LoyaltyWallet.Initial(), OpenLoyaltyWallet(
            new OpenLoyaltyWallet(WalletNumber, Owner, RedemptionLimit.Of(5), RedemptionCadence.Weekly),
            LoyaltyWallet.Initial()));

    public LoyaltyWallet.Active WalletOnWindow(int windowNumber) =>
        (LoyaltyWallet.Active)Apply(OpenWallet(), [
            new LoyaltyWalletEvent.RedemptionWindowProgressed(
                WalletNumber,
                Owner,
                windowNumber,
                LoyaltyPoints.Zero,
                RedemptionLimit.Of(5),
                [Owner])
        ]);

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
        public void OpensNotExistingWalletAndProgressesFirstRedemptionWindow()
        {
            fixture.Spec.Given()
                .When(new OpenLoyaltyWallet(fixture.WalletNumber, fixture.Owner, RedemptionLimit.Of(5), RedemptionCadence.Weekly))
                .Then(
                    new LoyaltyWalletEvent.LoyaltyWalletOpened(
                        fixture.WalletNumber,
                        fixture.Owner,
                        RedemptionCadence.Weekly,
                        RedemptionLimit.Of(5)),
                    new LoyaltyWalletEvent.RedemptionWindowProgressed(
                        fixture.WalletNumber,
                        fixture.Owner,
                        1,
                        LoyaltyPoints.Zero,
                        RedemptionLimit.Of(5),
                        [fixture.Owner]));
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
    }

    public class ProgressingRedemptionWindow(LoyaltyWalletFixture fixture): IClassFixture<LoyaltyWalletFixture>
    {
        [Fact]
        public void OpensTheNextWindowWhenCurrentWindowWasClosed()
        {
            var wallet = fixture.OpenWallet();
            var progressed = OpenNextRedemptionWindow(
                new OpenNextRedemptionWindow(fixture.WalletNumber, 1, LoyaltyPoints.Of(60)),
                wallet);
            var newState = (LoyaltyWallet.Active)LoyaltyWalletFixture.Apply(wallet, progressed);

            newState.CurrentWindowNumber.ShouldBe(2);
            var @event = progressed.ShouldHaveSingleItem().ShouldBeOfType<LoyaltyWalletEvent.RedemptionWindowProgressed>();
            @event.WalletNumber.ShouldBe(fixture.WalletNumber);
            @event.OwnerId.ShouldBe(fixture.Owner);
            @event.WindowNumber.ShouldBe(2);
            @event.OpeningBalance.ShouldBe(LoyaltyPoints.Of(60));
            @event.MaxRedemptionCount.ShouldBe(RedemptionLimit.Of(5));
            @event.Access.ShouldBe([fixture.Owner], ignoreOrder: true);
        }

        [Fact]
        public void IgnoresClosedWindowThatIsNotCurrent()
        {
            var wallet = fixture.WalletOnWindow(2);

            OpenNextRedemptionWindow(
                new OpenNextRedemptionWindow(fixture.WalletNumber, 1, LoyaltyPoints.Of(60)),
                wallet).ShouldBeEmpty();
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
            var newState = (LoyaltyWallet.Active)LoyaltyWalletFixture.Apply(opened, [cadenceSet]);

            newState.ShouldBe(opened with { Cadence = RedemptionCadence.Monthly });
            cadenceSet.ShouldBe(new LoyaltyWalletEvent.RedemptionCadenceSet(
                fixture.WalletNumber, fixture.Owner, RedemptionCadence.Monthly));
        }
    }

    public class Access(LoyaltyWalletFixture fixture): IClassFixture<LoyaltyWalletFixture>
    {
        [Fact]
        public void GrantsWalletAccess()
        {
            var wallet = fixture.OpenWallet();
            var granted = GrantWalletAccess(
                new GrantWalletAccess(fixture.WalletNumber, fixture.FamilyMember),
                wallet);
            var newState = (LoyaltyWallet.Active)LoyaltyWalletFixture.Apply(wallet, [granted]);

            newState.Access.ShouldBe(WalletAccess.Of(fixture.Owner, fixture.FamilyMember));
            granted.ShouldBe(new LoyaltyWalletEvent.WalletAccessGranted(fixture.WalletNumber, fixture.FamilyMember));
        }

        [Fact]
        public void RevokesWalletAccess()
        {
            var wallet = fixture.OpenWallet() with
            {
                Access = WalletAccess.Of(fixture.Owner, fixture.FamilyMember)
            };
            var revoked = RevokeWalletAccess(
                new RevokeWalletAccess(fixture.WalletNumber, fixture.FamilyMember),
                wallet);
            var newState = (LoyaltyWallet.Active)LoyaltyWalletFixture.Apply(wallet, [revoked]);

            newState.Access.ShouldBe(WalletAccess.Of(fixture.Owner));
            revoked.ShouldBe(new LoyaltyWalletEvent.WalletAccessRevoked(fixture.WalletNumber, fixture.FamilyMember));
        }
    }

    public class Lifecycle(LoyaltyWalletFixture fixture): IClassFixture<LoyaltyWalletFixture>
    {
        [Fact]
        public void DeactivatesActiveWallet()
        {
            var wallet = fixture.OpenWallet();
            var events = DeactivateWallet(wallet);
            var newState = (LoyaltyWallet.Deactivated)LoyaltyWalletFixture.Apply(wallet, events);

            newState.Status().ShouldBe("Deactivated");
            events.ShouldBe([new LoyaltyWalletEvent.WalletDeactivated(fixture.WalletNumber)]);
        }

        [Fact]
        public void ClosesExistingWallet()
        {
            var wallet = fixture.OpenWallet();
            var events = CloseWallet(wallet);
            var newState = (LoyaltyWallet.Closed)LoyaltyWalletFixture.Apply(wallet, events);

            newState.WalletNumber.ShouldBe(fixture.WalletNumber);
            events.ShouldBe([new LoyaltyWalletEvent.WalletClosed(fixture.WalletNumber)]);
        }
    }
}

internal static class LoyaltyWalletStateName
{
    public static string Status(this LoyaltyWallet wallet) =>
        wallet switch
        {
            LoyaltyWallet.NotExisting => "NotExisting",
            LoyaltyWallet.Active => "Active",
            LoyaltyWallet.Deactivated => "Deactivated",
            LoyaltyWallet.Closed => "Closed",
            _ => throw new ArgumentOutOfRangeException(nameof(wallet))
        };
}
