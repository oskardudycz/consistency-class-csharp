using ConsistencyClass.LoyaltyWallets.Access;
using ConsistencyClass.Membership;

namespace ConsistencyClass.LoyaltyWallets;

using static LoyaltyWallet;
using static LoyaltyWalletCommand;

public record WalletNumber(string Value)
{
    public static WalletNumber Of(string value) => new(value);
    public static WalletNumber Random() => new(Guid.NewGuid().ToString());
    public static WalletNumber ForOwner(MemberId ownerId) => Of($"wallet-{ownerId.Value}");
}

public enum RedemptionCadence { Weekly, Monthly }

public abstract record LoyaltyWalletEvent
{
    public record LoyaltyWalletOpened(
        WalletNumber WalletNumber,
        MemberId OwnerId,
        RedemptionCadence Cadence,
        RedemptionLimit MaxRedemptionCount): LoyaltyWalletEvent;

    public record RedemptionWindowProgressed(
        WalletNumber WalletNumber,
        MemberId OwnerId,
        int WindowNumber,
        LoyaltyPoints OpeningBalance,
        RedemptionLimit MaxRedemptionCount,
        IReadOnlyList<MemberId> Access): LoyaltyWalletEvent;

    public record RedemptionCadenceSet(
        WalletNumber WalletNumber,
        MemberId OwnerId,
        RedemptionCadence Cadence): LoyaltyWalletEvent;

    public record WalletAccessGranted(
        WalletNumber WalletNumber,
        MemberId MemberId): LoyaltyWalletEvent;

    public record WalletAccessRevoked(
        WalletNumber WalletNumber,
        MemberId MemberId): LoyaltyWalletEvent;

    public record WalletDeactivated(WalletNumber WalletNumber): LoyaltyWalletEvent;

    public record WalletClosed(WalletNumber WalletNumber): LoyaltyWalletEvent;

    private LoyaltyWalletEvent() { }
}

public abstract record LoyaltyWallet
{
    public record NotExisting: LoyaltyWallet;

    public record Active(
        WalletNumber WalletNumber,
        MemberId OwnerId,
        RedemptionCadence Cadence,
        RedemptionLimit MaxRedemptionCount,
        int CurrentWindowNumber,
        WalletAccess Access): LoyaltyWallet;

    public record Deactivated(
        WalletNumber WalletNumber,
        MemberId OwnerId,
        RedemptionCadence Cadence,
        RedemptionLimit MaxRedemptionCount,
        int CurrentWindowNumber,
        WalletAccess Access): LoyaltyWallet;

    public record Closed(WalletNumber WalletNumber): LoyaltyWallet;

    public static NotExisting Initial() => new();

    public static Active Open(
        WalletNumber walletNumber,
        MemberId ownerId,
        RedemptionCadence cadence,
        RedemptionLimit maxRedemptionCount) =>
        new(walletNumber, ownerId, cadence, maxRedemptionCount, 0, WalletAccess.Of(ownerId));

    private LoyaltyWallet() { }
}

public abstract record LoyaltyWalletCommand
{
    public record OpenLoyaltyWallet(
        WalletNumber WalletNumber,
        MemberId OwnerId,
        RedemptionLimit MaxRedemptionCount,
        RedemptionCadence Cadence): LoyaltyWalletCommand;

    public record OpenNextRedemptionWindow(
        WalletNumber WalletNumber,
        int ClosedWindowNumber,
        LoyaltyPoints ClosingBalance): LoyaltyWalletCommand;

    public record GrantWalletAccess(
        WalletNumber WalletNumber,
        MemberId MemberId): LoyaltyWalletCommand;

    public record RevokeWalletAccess(
        WalletNumber WalletNumber,
        MemberId MemberId): LoyaltyWalletCommand;

    public record SetRedemptionCadence(
        WalletNumber WalletNumber,
        RedemptionCadence Cadence): LoyaltyWalletCommand;

    public record DeactivateWallet(
        WalletNumber WalletNumber): LoyaltyWalletCommand;

    public record CloseWallet(
        WalletNumber WalletNumber): LoyaltyWalletCommand;

    private LoyaltyWalletCommand() { }
}

public static class LoyaltyWalletDecider
{
    public static LoyaltyWallet Evolve(LoyaltyWallet state, LoyaltyWalletEvent @event) =>
        @event switch
        {
            LoyaltyWalletEvent.LoyaltyWalletOpened e when state is NotExisting =>
                new Active(
                    e.WalletNumber,
                    e.OwnerId,
                    e.Cadence,
                    e.MaxRedemptionCount,
                    0,
                    WalletAccess.Of(e.OwnerId)),
            LoyaltyWalletEvent.RedemptionWindowProgressed e when state is Active wallet =>
                wallet with { CurrentWindowNumber = e.WindowNumber },
            LoyaltyWalletEvent.RedemptionCadenceSet e when state is not NotExisting and not Closed =>
                state switch
                {
                    Active wallet => wallet with { Cadence = e.Cadence },
                    Deactivated wallet => wallet with { Cadence = e.Cadence },
                    _ => state
                },
            LoyaltyWalletEvent.WalletAccessGranted e when state is not NotExisting and not Closed =>
                state switch
                {
                    Active wallet => wallet with { Access = wallet.Access.Add(e.MemberId) },
                    Deactivated wallet => wallet with { Access = wallet.Access.Add(e.MemberId) },
                    _ => state
                },
            LoyaltyWalletEvent.WalletAccessRevoked e when state is not NotExisting and not Closed =>
                state switch
                {
                    Active wallet => wallet with { Access = wallet.Access.Revoke(e.MemberId) },
                    Deactivated wallet => wallet with { Access = wallet.Access.Revoke(e.MemberId) },
                    _ => state
                },
            LoyaltyWalletEvent.WalletDeactivated when state is Active wallet =>
                new Deactivated(
                    wallet.WalletNumber,
                    wallet.OwnerId,
                    wallet.Cadence,
                    wallet.MaxRedemptionCount,
                    wallet.CurrentWindowNumber,
                    wallet.Access),
            LoyaltyWalletEvent.WalletClosed e => new Closed(e.WalletNumber),
            _ => state
        };

    public static IReadOnlyList<LoyaltyWalletEvent> Decide(
        LoyaltyWalletCommand command, LoyaltyWallet state)
    {
        switch (command)
        {
            case OpenLoyaltyWallet cmd:
                return OpenLoyaltyWallet(cmd, state);
            case OpenNextRedemptionWindow cmd:
                return OpenNextRedemptionWindow(cmd, state);
            case SetRedemptionCadence cmd:
                return [SetRedemptionCadence(cmd, state)];
            case GrantWalletAccess cmd:
                return [GrantWalletAccess(cmd, state)];
            case RevokeWalletAccess cmd:
                return [RevokeWalletAccess(cmd, state)];
            case DeactivateWallet _:
                return DeactivateWallet(state);
            case CloseWallet _:
                return CloseWallet(state);
            default:
                throw new ArgumentOutOfRangeException(nameof(command));
        }
    }

    public static IReadOnlyList<LoyaltyWalletEvent> OpenLoyaltyWallet(
        OpenLoyaltyWallet command,
        LoyaltyWallet state)
    {
        if (state is not NotExisting)
            return [];

        return
        [
            new LoyaltyWalletEvent.LoyaltyWalletOpened(
                command.WalletNumber,
                command.OwnerId,
                command.Cadence,
                command.MaxRedemptionCount),
            new LoyaltyWalletEvent.RedemptionWindowProgressed(
                command.WalletNumber,
                command.OwnerId,
                1,
                LoyaltyPoints.Zero,
                command.MaxRedemptionCount,
                [command.OwnerId])
        ];
    }

    public static IReadOnlyList<LoyaltyWalletEvent> OpenNextRedemptionWindow(
        OpenNextRedemptionWindow command,
        LoyaltyWallet state)
    {
        if (state is not Active wallet)
            return [];

        if (command.ClosedWindowNumber != wallet.CurrentWindowNumber)
            return [];

        return
        [
            new LoyaltyWalletEvent.RedemptionWindowProgressed(
                wallet.WalletNumber,
                wallet.OwnerId,
                wallet.CurrentWindowNumber + 1,
                command.ClosingBalance,
                wallet.MaxRedemptionCount,
                [.. wallet.Access.Members])
        ];
    }

    public static LoyaltyWalletEvent.RedemptionCadenceSet SetRedemptionCadence(
        SetRedemptionCadence command,
        LoyaltyWallet state)
    {
        var wallet = AssertActive(state);
        return new LoyaltyWalletEvent.RedemptionCadenceSet(wallet.WalletNumber, wallet.OwnerId, command.Cadence);
    }

    public static IReadOnlyList<LoyaltyWalletEvent> DeactivateWallet(LoyaltyWallet state)
    {
        if (state is Deactivated)
            return [];
        var wallet = AssertActive(state);
        return [new LoyaltyWalletEvent.WalletDeactivated(wallet.WalletNumber)];
    }

    public static IReadOnlyList<LoyaltyWalletEvent> CloseWallet(LoyaltyWallet state)
    {
        if (state is Closed)
            return [];
        if (state is NotExisting)
            throw new InvalidOperationException("Wallet doesn't exist");
        var walletNumber = state switch
        {
            Active a => a.WalletNumber,
            Deactivated d => d.WalletNumber,
            _ => throw new InvalidOperationException("Unexpected state")
        };
        return [new LoyaltyWalletEvent.WalletClosed(walletNumber)];
    }

    public static LoyaltyWalletEvent.WalletAccessGranted GrantWalletAccess(
        GrantWalletAccess command,
        LoyaltyWallet state)
    {
        var wallet = AssertActive(state);
        return new LoyaltyWalletEvent.WalletAccessGranted(wallet.WalletNumber, command.MemberId);
    }

    public static LoyaltyWalletEvent.WalletAccessRevoked RevokeWalletAccess(
        RevokeWalletAccess command,
        LoyaltyWallet state)
    {
        var wallet = AssertActive(state);
        return new LoyaltyWalletEvent.WalletAccessRevoked(wallet.WalletNumber, command.MemberId);
    }

    private static Active AssertActive(LoyaltyWallet state) =>
        state switch
        {
            Active active => active,
            NotExisting => throw new InvalidOperationException("Wallet doesn't exist"),
            Deactivated => throw new InvalidOperationException("Wallet is not active"),
            Closed => throw new InvalidOperationException("Wallet is closed"),
            _ => throw new InvalidOperationException("Unexpected state")
        };
}
