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
    public static (LoyaltyWallet State, LoyaltyWalletEvent[] Events) Decide(
        LoyaltyWalletCommand command, LoyaltyWallet state)
    {
        switch (command)
        {
            case OpenLoyaltyWallet cmd:
                return OpenLoyaltyWallet(cmd, state);
            case EarnLoyaltyPoints cmd:
                {
                    var (s, e) = EarnLoyaltyPoints(cmd, state);
                    return (s, [e]);
                }
            case RedeemLoyaltyPoints cmd:
                {
                    var (s, e) = RedeemLoyaltyPoints(cmd, state);
                    return (s, [e]);
                }
            case SetRedemptionCadence cmd:
                return (SetRedemptionCadence(cmd, state), []);
            case GrantWalletAccess cmd:
                return (GrantWalletAccess(cmd, state), []);
            case RevokeWalletAccess cmd:
                return (RevokeWalletAccess(cmd, state), []);
            case ResetRedemptionWindow cmd:
                {
                    var (s, e) = ResetRedemptionWindow(cmd, state);
                    return (s, [e]);
                }
            case DeactivateWallet _:
                return (DeactivateWallet(state), []);
            case CloseWallet _:
                return (CloseWallet(state), []);
            default:
                throw new ArgumentOutOfRangeException(nameof(command));
        }
    }

    public static (LoyaltyWallet State, LoyaltyWalletEvent[] Events) OpenLoyaltyWallet(
        OpenLoyaltyWallet command,
        LoyaltyWallet state)
    {
        if (state is not NotExisting)
            return (state, []);

        var wallet = Open(command.WalletNumber, command.OwnerId, command.Cadence, command.MaxRedemptionCount);
        return (wallet, [new LoyaltyWalletEvent.LoyaltyWalletOpened(
            wallet.WalletNumber,
            wallet.OwnerId,
            wallet.Cadence,
            command.MaxRedemptionCount,
            wallet.PointsLimit.EarnedPoints,
            wallet.PointsLimit.RedeemedPoints)]);
    }

    public static (LoyaltyWallet.Active State, LoyaltyWalletEvent.LoyaltyPointsEarned Event) EarnLoyaltyPoints(
        EarnLoyaltyPoints command,
        LoyaltyWallet state)
    {
        var wallet = AssertActive(state);
        var earned = wallet with { PointsLimit = wallet.PointsLimit.Earn(command.Points) };
        return (earned, new LoyaltyWalletEvent.LoyaltyPointsEarned(
            wallet.WalletNumber, wallet.OwnerId, command.Points, command.At));
    }

    public static (LoyaltyWallet.Active State, LoyaltyWalletEvent.LoyaltyPointsRedeemed Event) RedeemLoyaltyPoints(
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

        return (wallet with { PointsLimit = wallet.PointsLimit.Redeem(burned) },
            new LoyaltyWalletEvent.LoyaltyPointsRedeemed(
                wallet.WalletNumber, wallet.OwnerId, command.MemberId,
                command.Points, burned, command.At));
    }

    public static Active SetRedemptionCadence(
        SetRedemptionCadence command,
        LoyaltyWallet state)
    {
        var wallet = AssertActive(state);
        return wallet with { Cadence = command.Cadence };
    }

    public static (LoyaltyWallet.Active State, LoyaltyWalletEvent.RedemptionWindowReset Event) ResetRedemptionWindow(
        ResetRedemptionWindow command,
        LoyaltyWallet state)
    {
        var wallet = AssertActive(state);
        return (wallet with { PointsLimit = wallet.PointsLimit.ResetRedemptionCount() },
            new LoyaltyWalletEvent.RedemptionWindowReset(wallet.WalletNumber, wallet.OwnerId, command.At));
    }

    public static LoyaltyWallet DeactivateWallet(LoyaltyWallet state)
    {
        if (state is Deactivated)
            return state;
        var wallet = AssertActive(state);
        return new Deactivated(wallet.WalletNumber, wallet.OwnerId, wallet.PointsLimit, wallet.Cadence, wallet.Access);
    }

    public static LoyaltyWallet CloseWallet(LoyaltyWallet state)
    {
        if (state is Closed)
            return state;
        if (state is NotExisting)
            throw new InvalidOperationException("Wallet doesn't exist");
        var walletNumber = state switch
        {
            Active a => a.WalletNumber,
            Deactivated d => d.WalletNumber,
            _ => throw new InvalidOperationException("Unexpected state")
        };
        return new Closed(walletNumber);
    }

    public static Active GrantWalletAccess(
        GrantWalletAccess command,
        LoyaltyWallet state)
    {
        var wallet = AssertActive(state);
        return wallet with { Access = wallet.Access.Add(command.MemberId) };
    }

    public static Active RevokeWalletAccess(
        RevokeWalletAccess command,
        LoyaltyWallet state)
    {
        var wallet = AssertActive(state);
        return wallet with { Access = wallet.Access.Revoke(command.MemberId) };
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
