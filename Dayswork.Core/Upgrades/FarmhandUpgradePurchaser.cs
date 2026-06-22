namespace Dayswork.Core.Upgrades;

public static class FarmhandUpgradePurchaser
{
    public static FarmhandUpgradePurchaseResult TryPurchase(
        FarmhandUpgradeKind kind,
        FarmhandUpgradeState state,
        int availableGold)
    {
        var definition = FarmhandUpgradeCatalog.Get(kind);

        if (state.IsPurchased(kind))
        {
            return new FarmhandUpgradePurchaseResult(
                FarmhandUpgradePurchaseStatus.AlreadyPurchased,
                state,
                availableGold,
                definition.Price);
        }

        if (availableGold < definition.Price)
        {
            return new FarmhandUpgradePurchaseResult(
                FarmhandUpgradePurchaseStatus.InsufficientFunds,
                state,
                availableGold,
                definition.Price);
        }

        return new FarmhandUpgradePurchaseResult(
            FarmhandUpgradePurchaseStatus.Purchased,
            state.MarkPurchased(kind),
            availableGold - definition.Price,
            definition.Price);
    }
}
