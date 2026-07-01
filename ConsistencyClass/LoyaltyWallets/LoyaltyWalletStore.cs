namespace ConsistencyClass.LoyaltyWallets;

using ConsistencyClass.Core;

public delegate ValueTask<LoyaltyWallet> GetLoyaltyWallet(WalletNumber walletNumber);
public delegate ValueTask SaveLoyaltyWallet(WalletNumber walletNumber, IReadOnlyList<LoyaltyWalletEvent> events);

internal class LoyaltyWalletStore
{
    private readonly EventStore<LoyaltyWalletEvent> eventStore;

    public LoyaltyWalletStore()
    {
        eventStore = new EventStore<LoyaltyWalletEvent>([]);
    }

    public async ValueTask<LoyaltyWallet> GetLoyaltyWallet(WalletNumber walletNumber)
    {
        var events = await eventStore.ReadEvents<LoyaltyWalletEvent>(walletNumber.Value);
        return events.Aggregate(
            (LoyaltyWallet)LoyaltyWallet.Initial(),
            (wallet, @event) => LoyaltyWalletDecider.Evolve(wallet, @event));
    }

    public async ValueTask SaveLoyaltyWallet(WalletNumber walletNumber, IReadOnlyList<LoyaltyWalletEvent> events)
    {
        if (events.Count == 0)
            return;

        await eventStore.AppendToStream(walletNumber.Value, events);
    }
}
