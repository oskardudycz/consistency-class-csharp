using ConsistencyClass.LoyaltyWallets;

namespace ConsistencyClass.Tests.LoyaltyWallets;

internal static class LoyaltyWalletStoreFixture
{
    public static LoyaltyWalletStore CreateStore() =>
        new();
}
