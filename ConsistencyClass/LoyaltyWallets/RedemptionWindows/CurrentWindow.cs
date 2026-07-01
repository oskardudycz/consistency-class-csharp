using ConsistencyClass.Core;
using ConsistencyClass.Core.Projections;
using ConsistencyClass.Membership;

namespace ConsistencyClass.LoyaltyWallets.RedemptionWindows;

using static RedemptionWindowEvent;

public record CurrentWindow(
    WalletNumber WalletNumber,
    MemberId OwnerId,
    int WindowNumber,
    bool Open)
{
    public static CurrentWindow? Evolve(CurrentWindow? document, RedemptionWindowEvent @event) =>
        @event switch
        {
            RedemptionWindowOpened opened => new CurrentWindow(
                opened.WalletNumber,
                opened.OwnerId,
                opened.WindowNumber,
                true),
            RedemptionWindowClosed closed when document is not null && document.WindowNumber == closed.WindowNumber =>
                document with { Open = false },
            _ => document
        };

    public static Projection<CurrentWindow, RedemptionWindowEvent> Projection(
        DatabaseCollection<CurrentWindow> collection) =>
        new(
            collection,
            new HashSet<Type>
            {
                typeof(RedemptionWindowOpened),
                typeof(RedemptionWindowClosed)
            },
            WalletNumberOf,
            Evolve);

    private static string WalletNumberOf(RedemptionWindowEvent @event) =>
        @event switch
        {
            RedemptionWindowOpened e => e.WalletNumber.Value,
            RedemptionWindowClosed e => e.WalletNumber.Value,
            _ => throw new ArgumentOutOfRangeException(nameof(@event))
        };
}
