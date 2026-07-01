using ConsistencyClass.LoyaltyWallets.Access;
using ConsistencyClass.Membership;

namespace ConsistencyClass.LoyaltyWallets.RedemptionWindows;

using static RedemptionWindow;
using static RedemptionWindowCommand;

public abstract record RedemptionWindow
{
    public record NotOpened: RedemptionWindow;

    public record Open(
        WalletNumber WalletNumber,
        MemberId OwnerId,
        int WindowNumber,
        LoyaltyPoints OpeningBalance,
        LoyaltyPointsLimit PointsLimit,
        WalletAccess Access): RedemptionWindow;

    public record Closed(
        WalletNumber WalletNumber,
        MemberId OwnerId,
        int WindowNumber,
        LoyaltyPoints ClosingBalance,
        RedemptionLimit RedemptionCount): RedemptionWindow;

    public static NotOpened Initial() => new();

    private RedemptionWindow() { }
}

public abstract record RedemptionWindowEvent
{
    public record RedemptionWindowOpened(
        WalletNumber WalletNumber,
        MemberId OwnerId,
        int WindowNumber,
        LoyaltyPoints OpeningBalance,
        RedemptionLimit MaxRedemptionCount,
        IReadOnlyList<MemberId> Access): RedemptionWindowEvent;

    public record LoyaltyPointsEarned(
        WalletNumber WalletNumber,
        MemberId OwnerId,
        int WindowNumber,
        LoyaltyPoints Points,
        DateTime At): RedemptionWindowEvent;

    public record LoyaltyPointsRedeemed(
        WalletNumber WalletNumber,
        MemberId OwnerId,
        int WindowNumber,
        MemberId ByMemberId,
        LoyaltyPoints Points,
        LoyaltyPoints Burned,
        DateTime At): RedemptionWindowEvent;

    public record WindowAccessGranted(
        WalletNumber WalletNumber,
        MemberId OwnerId,
        int WindowNumber,
        MemberId MemberId): RedemptionWindowEvent;

    public record WindowAccessRevoked(
        WalletNumber WalletNumber,
        MemberId OwnerId,
        int WindowNumber,
        MemberId MemberId): RedemptionWindowEvent;

    public record RedemptionWindowClosed(
        WalletNumber WalletNumber,
        MemberId OwnerId,
        int WindowNumber,
        LoyaltyPoints ClosingBalance,
        RedemptionLimit RedemptionCount,
        bool HadActivity,
        DateTime ClosedAt): RedemptionWindowEvent;

    private RedemptionWindowEvent() { }
}

public abstract record RedemptionWindowCommand
{
    public record OpenRedemptionWindow(
        WalletNumber WalletNumber,
        MemberId OwnerId,
        int WindowNumber,
        LoyaltyPoints OpeningBalance,
        RedemptionLimit MaxRedemptionCount,
        IReadOnlyList<MemberId> Access): RedemptionWindowCommand;

    public record EarnLoyaltyPoints(
        WalletNumber WalletNumber,
        LoyaltyPoints Points,
        DateTime At): RedemptionWindowCommand;

    public record RedeemLoyaltyPoints(
        WalletNumber WalletNumber,
        MemberId MemberId,
        LoyaltyPoints Points,
        DateTime At,
        LoyaltyPoints? Burned = null): RedemptionWindowCommand;

    public record GrantWindowAccess(
        WalletNumber WalletNumber,
        MemberId MemberId): RedemptionWindowCommand;

    public record RevokeWindowAccess(
        WalletNumber WalletNumber,
        MemberId MemberId): RedemptionWindowCommand;

    public record CloseRedemptionWindow(
        WalletNumber WalletNumber,
        DateTime ClosedAt): RedemptionWindowCommand;

    private RedemptionWindowCommand() { }
}

public static class RedemptionWindowStream
{
    public static string Of(WalletNumber walletNumber, int windowNumber) =>
        $"redemptionWindow-{walletNumber.Value}-{windowNumber}";
}

public static class RedemptionWindowDecider
{
    public static LoyaltyPoints AvailableBalance(Open window) =>
        LoyaltyPoints.Of(window.OpeningBalance.Value + window.PointsLimit.AvailablePoints.Value);

    public static RedemptionLimit RedemptionsLeft(Open window) =>
        RedemptionLimit.Of(window.PointsLimit.RedemptionsLeft);

    public static RedemptionWindow Evolve(RedemptionWindow state, RedemptionWindowEvent @event) =>
        @event switch
        {
            RedemptionWindowEvent.RedemptionWindowOpened e when state is NotOpened =>
                new Open(
                    e.WalletNumber,
                    e.OwnerId,
                    e.WindowNumber,
                    e.OpeningBalance,
                    LoyaltyPointsLimit.Initial(e.MaxRedemptionCount),
                    WalletAccess.Of([.. e.Access])),
            RedemptionWindowEvent.LoyaltyPointsEarned e when state is Open window =>
                window with { PointsLimit = window.PointsLimit.Earn(e.Points) },
            RedemptionWindowEvent.LoyaltyPointsRedeemed e when state is Open window =>
                window with { PointsLimit = window.PointsLimit.Redeem(e.Burned) },
            RedemptionWindowEvent.WindowAccessGranted e when state is Open window =>
                window with { Access = window.Access.Add(e.MemberId) },
            RedemptionWindowEvent.WindowAccessRevoked e when state is Open window =>
                window with { Access = window.Access.Revoke(e.MemberId) },
            RedemptionWindowEvent.RedemptionWindowClosed e when state is Open window =>
                new Closed(
                    window.WalletNumber,
                    window.OwnerId,
                    window.WindowNumber,
                    e.ClosingBalance,
                    e.RedemptionCount),
            _ => state
        };

    public static IReadOnlyList<RedemptionWindowEvent> Decide(
        RedemptionWindowCommand command, RedemptionWindow state)
    {
        switch (command)
        {
            case OpenRedemptionWindow cmd:
                return OpenRedemptionWindow(cmd, state);
            case EarnLoyaltyPoints cmd:
                return [EarnLoyaltyPoints(cmd, state)];
            case RedeemLoyaltyPoints cmd:
                return [RedeemLoyaltyPoints(cmd, state)];
            case GrantWindowAccess cmd:
                return GrantWindowAccess(cmd, state);
            case RevokeWindowAccess cmd:
                return RevokeWindowAccess(cmd, state);
            case CloseRedemptionWindow cmd:
                return CloseRedemptionWindow(cmd, state);
            default:
                throw new ArgumentOutOfRangeException(nameof(command));
        }
    }

    public static IReadOnlyList<RedemptionWindowEvent> OpenRedemptionWindow(
        OpenRedemptionWindow command,
        RedemptionWindow state)
    {
        if (state is not NotOpened)
            return [];

        return
        [
            new RedemptionWindowEvent.RedemptionWindowOpened(
                command.WalletNumber,
                command.OwnerId,
                command.WindowNumber,
                command.OpeningBalance,
                command.MaxRedemptionCount,
                command.Access)
        ];
    }

    public static RedemptionWindowEvent.LoyaltyPointsEarned EarnLoyaltyPoints(
        EarnLoyaltyPoints command,
        RedemptionWindow state)
    {
        var window = AssertOpen(state);
        return new RedemptionWindowEvent.LoyaltyPointsEarned(
            window.WalletNumber,
            window.OwnerId,
            window.WindowNumber,
            command.Points,
            command.At);
    }

    public static RedemptionWindowEvent.LoyaltyPointsRedeemed RedeemLoyaltyPoints(
        RedeemLoyaltyPoints command,
        RedemptionWindow state)
    {
        var window = AssertOpen(state);
        var burned = command.Burned ?? command.Points;

        if (!window.Access.Has(command.MemberId))
            throw new InvalidOperationException("Not authorized to redeem");

        if (window.PointsLimit.RedemptionsLeft <= 0)
            throw new InvalidOperationException("Redemption window exhausted");

        if (AvailableBalance(window).Value < burned.Value)
            throw new InvalidOperationException("Not enough points to redeem");

        return new RedemptionWindowEvent.LoyaltyPointsRedeemed(
            window.WalletNumber,
            window.OwnerId,
            window.WindowNumber,
            command.MemberId,
            command.Points,
            burned,
            command.At);
    }

    public static IReadOnlyList<RedemptionWindowEvent> GrantWindowAccess(
        GrantWindowAccess command,
        RedemptionWindow state)
    {
        if (state is not Open window)
            return [];

        return
        [
            new RedemptionWindowEvent.WindowAccessGranted(
                window.WalletNumber,
                window.OwnerId,
                window.WindowNumber,
                command.MemberId)
        ];
    }

    public static IReadOnlyList<RedemptionWindowEvent> RevokeWindowAccess(
        RevokeWindowAccess command,
        RedemptionWindow state)
    {
        if (state is not Open window)
            return [];

        return
        [
            new RedemptionWindowEvent.WindowAccessRevoked(
                window.WalletNumber,
                window.OwnerId,
                window.WindowNumber,
                command.MemberId)
        ];
    }

    public static IReadOnlyList<RedemptionWindowEvent> CloseRedemptionWindow(
        CloseRedemptionWindow command,
        RedemptionWindow state)
    {
        if (state is not Open window)
            return [];

        var hadActivity =
            window.PointsLimit.EarnedPoints.Value > 0 || window.PointsLimit.RedemptionCount.Value > 0;

        return
        [
            new RedemptionWindowEvent.RedemptionWindowClosed(
                window.WalletNumber,
                window.OwnerId,
                window.WindowNumber,
                AvailableBalance(window),
                window.PointsLimit.RedemptionCount,
                hadActivity,
                command.ClosedAt)
        ];
    }

    private static Open AssertOpen(RedemptionWindow state) =>
        state switch
        {
            Open open => open,
            NotOpened => throw new InvalidOperationException("Redemption window is not open"),
            Closed => throw new InvalidOperationException("Redemption window is closed"),
            _ => throw new InvalidOperationException("Unexpected state")
        };
}
