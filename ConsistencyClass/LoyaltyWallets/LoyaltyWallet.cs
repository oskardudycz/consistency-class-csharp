namespace ConsistencyClass.LoyaltyWallets;

using static LoyaltyWallet;
using static LoyaltyWalletCommand;

public record WalletNumber(string Value)
{
    public static WalletNumber Of(string value) => new(value);
    public static WalletNumber Random() => new(Guid.NewGuid().ToString());
}

public enum RedemptionCadence { Weekly, Monthly }

public abstract record LoyaltyWallet
{
    public record NotExisting: LoyaltyWallet;

    public record Active(
        WalletNumber WalletNumber,
        LoyaltyPointsLimit PointsLimit,
        RedemptionCadence Cadence): LoyaltyWallet;

    public record Deactivated(
        WalletNumber WalletNumber,
        LoyaltyPointsLimit PointsLimit,
        RedemptionCadence Cadence): LoyaltyWallet;

    public record Closed: LoyaltyWallet;

    public static NotExisting Initial() => new();

    public static Active Open(
        WalletNumber walletNumber,
        RedemptionCadence cadence,
        RedemptionLimit maxRedemptionCount) =>
        new(walletNumber, LoyaltyPointsLimit.Initial(maxRedemptionCount), cadence);

    private LoyaltyWallet() { }
}

public abstract record LoyaltyWalletCommand
{
    public record OpenLoyaltyWallet(
        WalletNumber WalletNumber,
        RedemptionLimit MaxRedemptionCount,
        RedemptionCadence Cadence): LoyaltyWalletCommand;

    public record EarnLoyaltyPoints(
        WalletNumber WalletNumber,
        LoyaltyPoints Points): LoyaltyWalletCommand;

    public record RedeemLoyaltyPoints(
        WalletNumber WalletNumber,
        LoyaltyPoints Points): LoyaltyWalletCommand;

    public record SetRedemptionCadence(
        WalletNumber WalletNumber,
        RedemptionCadence Cadence): LoyaltyWalletCommand;

    public record ResetRedemptionWindow(
        WalletNumber WalletNumber): LoyaltyWalletCommand;

    public record DeactivateWallet(
        WalletNumber WalletNumber): LoyaltyWalletCommand;

    public record CloseWallet(
        WalletNumber WalletNumber): LoyaltyWalletCommand;

    private LoyaltyWalletCommand() { }
}

public static class LoyaltyWalletDecider
{
    public static LoyaltyWallet Decide(LoyaltyWalletCommand command, LoyaltyWallet state) =>
        command switch
        {
            OpenLoyaltyWallet cmd => OpenLoyaltyWallet(cmd, state),
            EarnLoyaltyPoints cmd => EarnLoyaltyPoints(cmd, state),
            RedeemLoyaltyPoints cmd => RedeemLoyaltyPoints(cmd, state),
            SetRedemptionCadence cmd => SetRedemptionCadence(cmd, state),
            ResetRedemptionWindow _ => ResetRedemptionWindow(state),
            DeactivateWallet _ => DeactivateWallet(state),
            CloseWallet _ => CloseWallet(state),
            _ => throw new ArgumentOutOfRangeException(nameof(command))
        };

    public static LoyaltyWallet OpenLoyaltyWallet(
        OpenLoyaltyWallet command,
        LoyaltyWallet state) =>
        state is NotExisting
            ? Open(command.WalletNumber, command.Cadence, command.MaxRedemptionCount)
            : state;

    public static Active EarnLoyaltyPoints(
        EarnLoyaltyPoints command,
        LoyaltyWallet state)
    {
        var wallet = AssertActive(state);
        return wallet with { PointsLimit = wallet.PointsLimit.Earn(command.Points) };
    }

    public static Active RedeemLoyaltyPoints(
        RedeemLoyaltyPoints command,
        LoyaltyWallet state)
    {
        var wallet = AssertActive(state);

        if (wallet.PointsLimit.RedemptionsLeft <= 0)
            throw new InvalidOperationException("Redemption window exhausted");

        if (wallet.PointsLimit.AvailablePoints.Value < command.Points.Value)
            throw new InvalidOperationException("Not enough points to redeem");

        return wallet with { PointsLimit = wallet.PointsLimit.Redeem(command.Points) };
    }

    public static Active SetRedemptionCadence(
        SetRedemptionCadence command,
        LoyaltyWallet state)
    {
        var wallet = AssertActive(state);
        return wallet with { Cadence = command.Cadence };
    }

    public static Active ResetRedemptionWindow(LoyaltyWallet state)
    {
        var wallet = AssertActive(state);
        return wallet with { PointsLimit = wallet.PointsLimit.ResetRedemptionCount() };
    }

    public static LoyaltyWallet DeactivateWallet(LoyaltyWallet state)
    {
        if (state is Deactivated)
            return state;
        var wallet = AssertActive(state);
        return new Deactivated(wallet.WalletNumber, wallet.PointsLimit, wallet.Cadence);
    }

    public static LoyaltyWallet CloseWallet(LoyaltyWallet state)
    {
        if (state is Closed)
            return state;
        if (state is NotExisting)
            throw new InvalidOperationException("Wallet doesn't exist");
        return new Closed();
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
