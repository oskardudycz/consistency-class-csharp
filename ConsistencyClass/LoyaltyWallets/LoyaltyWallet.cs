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
        RedemptionLimit MaxRedemptionCount,
        LoyaltyPoints EarnedPoints,
        LoyaltyPoints RedeemedPoints): LoyaltyWalletEvent;

    public record LoyaltyPointsEarned(
        WalletNumber WalletNumber,
        MemberId OwnerId,
        LoyaltyPoints Points,
        DateTime At): LoyaltyWalletEvent;

    public record LoyaltyPointsRedeemed(
        WalletNumber WalletNumber,
        MemberId OwnerId,
        MemberId ByMemberId,
        LoyaltyPoints Points,
        LoyaltyPoints Burned,
        DateTime At): LoyaltyWalletEvent;

    public record RedemptionWindowReset(
        WalletNumber WalletNumber,
        MemberId OwnerId,
        DateTime At): LoyaltyWalletEvent;

    public record RedemptionCadenceSet(
        WalletNumber WalletNumber,
        MemberId OwnerId,
        RedemptionCadence Cadence): LoyaltyWalletEvent;

    public record WalletAccessGranted(
        WalletNumber WalletNumber,
        MemberId OwnerId,
        MemberId MemberId): LoyaltyWalletEvent;

    public record WalletAccessRevoked(
        WalletNumber WalletNumber,
        MemberId OwnerId,
        MemberId MemberId): LoyaltyWalletEvent;

    public record WalletDeactivated(
        WalletNumber WalletNumber,
        MemberId OwnerId): LoyaltyWalletEvent;

    public record WalletClosed(WalletNumber WalletNumber): LoyaltyWalletEvent;

    private LoyaltyWalletEvent() { }
}

public abstract record LoyaltyWallet
{
    public record NotExisting: LoyaltyWallet;

    public record Active(
        WalletNumber WalletNumber,
        MemberId OwnerId,
        LoyaltyPointsLimit PointsLimit,
        RedemptionCadence Cadence,
        WalletAccess Access): LoyaltyWallet;

    public record Deactivated(
        WalletNumber WalletNumber,
        MemberId OwnerId,
        LoyaltyPointsLimit PointsLimit,
        RedemptionCadence Cadence,
        WalletAccess Access): LoyaltyWallet;

    public record Closed(WalletNumber WalletNumber): LoyaltyWallet;

    public static NotExisting Initial() => new();

    public static Active Open(
        WalletNumber walletNumber,
        MemberId ownerId,
        RedemptionCadence cadence,
        RedemptionLimit maxRedemptionCount) =>
        new(walletNumber, ownerId, LoyaltyPointsLimit.Initial(maxRedemptionCount), cadence, WalletAccess.Of(ownerId));

    private LoyaltyWallet() { }
}

public abstract record LoyaltyWalletCommand
{
    public record OpenLoyaltyWallet(
        WalletNumber WalletNumber,
        MemberId OwnerId,
        RedemptionLimit MaxRedemptionCount,
        RedemptionCadence Cadence): LoyaltyWalletCommand;

    public record EarnLoyaltyPoints(
        WalletNumber WalletNumber,
        LoyaltyPoints Points,
        DateTime At): LoyaltyWalletCommand;

    public record RedeemLoyaltyPoints(
        WalletNumber WalletNumber,
        MemberId MemberId,
        LoyaltyPoints Points,
        DateTime At,
        LoyaltyPoints? Burned = null): LoyaltyWalletCommand;

    public record GrantWalletAccess(
        WalletNumber WalletNumber,
        MemberId MemberId): LoyaltyWalletCommand;

    public record RevokeWalletAccess(
        WalletNumber WalletNumber,
        MemberId MemberId): LoyaltyWalletCommand;

    public record SetRedemptionCadence(
        WalletNumber WalletNumber,
        RedemptionCadence Cadence): LoyaltyWalletCommand;

    public record ResetRedemptionWindow(
        WalletNumber WalletNumber,
        DateTime At): LoyaltyWalletCommand;

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
                    LoyaltyPointsLimit.Of(
                        e.EarnedPoints,
                        e.RedeemedPoints,
                        RedemptionLimit.Zero,
                        e.MaxRedemptionCount),
                    e.Cadence,
                    WalletAccess.Of(e.OwnerId)),
            LoyaltyWalletEvent.LoyaltyPointsEarned e when state is Active wallet =>
                wallet with { PointsLimit = wallet.PointsLimit.Earn(e.Points) },
            LoyaltyWalletEvent.LoyaltyPointsRedeemed e when state is Active wallet =>
                wallet with { PointsLimit = wallet.PointsLimit.Redeem(e.Burned) },
            LoyaltyWalletEvent.RedemptionWindowReset when state is Active wallet =>
                wallet with { PointsLimit = wallet.PointsLimit.ResetRedemptionCount() },
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
                new Deactivated(wallet.WalletNumber, wallet.OwnerId, wallet.PointsLimit, wallet.Cadence, wallet.Access),
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
            case EarnLoyaltyPoints cmd:
                return [EarnLoyaltyPoints(cmd, state)];
            case RedeemLoyaltyPoints cmd:
                return [RedeemLoyaltyPoints(cmd, state)];
            case SetRedemptionCadence cmd:
                return [SetRedemptionCadence(cmd, state)];
            case GrantWalletAccess cmd:
                return [GrantWalletAccess(cmd, state)];
            case RevokeWalletAccess cmd:
                return [RevokeWalletAccess(cmd, state)];
            case ResetRedemptionWindow cmd:
                return [ResetRedemptionWindow(cmd, state)];
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

        var wallet = Open(command.WalletNumber, command.OwnerId, command.Cadence, command.MaxRedemptionCount);
        return [new LoyaltyWalletEvent.LoyaltyWalletOpened(
            wallet.WalletNumber,
            wallet.OwnerId,
            wallet.Cadence,
            command.MaxRedemptionCount,
            wallet.PointsLimit.EarnedPoints,
            wallet.PointsLimit.RedeemedPoints)];
    }

    public static LoyaltyWalletEvent.LoyaltyPointsEarned EarnLoyaltyPoints(
        EarnLoyaltyPoints command,
        LoyaltyWallet state)
    {
        var wallet = AssertActive(state);
        return new LoyaltyWalletEvent.LoyaltyPointsEarned(wallet.WalletNumber, wallet.OwnerId, command.Points, command.At);
    }

    public static LoyaltyWalletEvent.LoyaltyPointsRedeemed RedeemLoyaltyPoints(
        RedeemLoyaltyPoints command,
        LoyaltyWallet state)
    {
        var wallet = AssertActive(state);
        var burned = command.Burned ?? command.Points;

        if (!wallet.Access.Has(command.MemberId))
            throw new InvalidOperationException("Not authorized to redeem");

        if (wallet.PointsLimit.RedemptionsLeft <= 0)
            throw new InvalidOperationException("Redemption window exhausted");

        if (wallet.PointsLimit.AvailablePoints.Value < burned.Value)
            throw new InvalidOperationException("Not enough points to redeem");

        return new LoyaltyWalletEvent.LoyaltyPointsRedeemed(
            wallet.WalletNumber, wallet.OwnerId, command.MemberId,
            command.Points, burned, command.At);
    }

    public static LoyaltyWalletEvent.RedemptionCadenceSet SetRedemptionCadence(
        SetRedemptionCadence command,
        LoyaltyWallet state)
    {
        var wallet = AssertActive(state);
        return new LoyaltyWalletEvent.RedemptionCadenceSet(wallet.WalletNumber, wallet.OwnerId, command.Cadence);
    }

    public static LoyaltyWalletEvent.RedemptionWindowReset ResetRedemptionWindow(
        ResetRedemptionWindow command,
        LoyaltyWallet state)
    {
        var wallet = AssertActive(state);
        return new LoyaltyWalletEvent.RedemptionWindowReset(wallet.WalletNumber, wallet.OwnerId, command.At);
    }

    public static IReadOnlyList<LoyaltyWalletEvent> DeactivateWallet(LoyaltyWallet state)
    {
        if (state is Deactivated)
            return [];
        var wallet = AssertActive(state);
        return [new LoyaltyWalletEvent.WalletDeactivated(wallet.WalletNumber, wallet.OwnerId)];
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
        return new LoyaltyWalletEvent.WalletAccessGranted(wallet.WalletNumber, wallet.OwnerId, command.MemberId);
    }

    public static LoyaltyWalletEvent.WalletAccessRevoked RevokeWalletAccess(
        RevokeWalletAccess command,
        LoyaltyWallet state)
    {
        var wallet = AssertActive(state);
        return new LoyaltyWalletEvent.WalletAccessRevoked(wallet.WalletNumber, wallet.OwnerId, command.MemberId);
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
