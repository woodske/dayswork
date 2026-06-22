namespace Dayswork.Tests.Upgrades;

using Dayswork.Core.Upgrades;
using Xunit;

public sealed class FarmhandUpgradePurchaserTests
{
    [Fact]
    public void TryPurchase_AffordablePurchase_DeductsPriceAndMarksPurchased()
    {
        var result = FarmhandUpgradePurchaser.TryPurchase(
            FarmhandUpgradeKind.Speed,
            FarmhandUpgradeState.Empty,
            availableGold: 20000);

        Assert.Equal(FarmhandUpgradePurchaseStatus.Purchased, result.Status);
        Assert.True(result.State.SpeedPurchased);
        Assert.Equal(5000, result.RemainingGold);
    }

    [Fact]
    public void TryPurchase_InsufficientFunds_LeavesStateAndGoldUnchanged()
    {
        var result = FarmhandUpgradePurchaser.TryPurchase(
            FarmhandUpgradeKind.Energy,
            FarmhandUpgradeState.Empty,
            availableGold: 9999);

        Assert.Equal(FarmhandUpgradePurchaseStatus.InsufficientFunds, result.Status);
        Assert.Equal(FarmhandUpgradeState.Empty, result.State);
        Assert.Equal(9999, result.RemainingGold);
    }

    [Fact]
    public void TryPurchase_AlreadyPurchased_LeavesStateAndGoldUnchanged()
    {
        var state = FarmhandUpgradeState.Empty.MarkPurchased(FarmhandUpgradeKind.Energy);

        var result = FarmhandUpgradePurchaser.TryPurchase(
            FarmhandUpgradeKind.Energy,
            state,
            availableGold: 20000);

        Assert.Equal(FarmhandUpgradePurchaseStatus.AlreadyPurchased, result.Status);
        Assert.Equal(state, result.State);
        Assert.Equal(20000, result.RemainingGold);
    }

    [Fact]
    public void Catalog_UsesRequestedPrices()
    {
        Assert.Equal(15000, FarmhandUpgradeCatalog.Get(FarmhandUpgradeKind.Speed).Price);
        Assert.Equal(10000, FarmhandUpgradeCatalog.Get(FarmhandUpgradeKind.Energy).Price);
    }
}
