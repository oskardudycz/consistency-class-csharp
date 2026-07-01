using ConsistencyClass.LoyaltyWallets;
using WalletDetailsDocument = ConsistencyClass.LoyaltyWallets.WalletDetails.WalletDetails;

namespace ConsistencyClass.Tests.LoyaltyWallets;

internal static class LoyaltyWalletStoreFixture
{
    public static LoyaltyWalletStore CreateStore() =>
        new(Database.Collection<WalletDetailsDocument>());
}
