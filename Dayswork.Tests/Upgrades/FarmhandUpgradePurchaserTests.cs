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
    public void TryPurchase_Speed2WithoutFirstSpeed_ReturnsPrerequisiteNotMet()
    {
        var result = FarmhandUpgradePurchaser.TryPurchase(
            FarmhandUpgradeKind.Speed2,
            FarmhandUpgradeState.Empty,
            availableGold: 100000);

        Assert.Equal(FarmhandUpgradePurchaseStatus.PrerequisiteNotMet, result.Status);
        Assert.Equal(FarmhandUpgradeState.Empty, result.State);
        Assert.Equal(100000, result.RemainingGold);
    }

    [Fact]
    public void TryPurchase_Speed2WithFirstSpeed_DeductsPriceAndMarksPurchased()
    {
        var state = FarmhandUpgradeState.Empty.MarkPurchased(FarmhandUpgradeKind.Speed);

        var result = FarmhandUpgradePurchaser.TryPurchase(
            FarmhandUpgradeKind.Speed2,
            state,
            availableGold: 40000);

        Assert.Equal(FarmhandUpgradePurchaseStatus.Purchased, result.Status);
        Assert.True(result.State.Speed2Purchased);
        Assert.Equal(5000, result.RemainingGold);
    }

    [Fact]
    public void TryPurchase_Speed2PrerequisiteMetButUnaffordable_ReturnsInsufficientFunds()
    {
        var state = FarmhandUpgradeState.Empty.MarkPurchased(FarmhandUpgradeKind.Speed);

        var result = FarmhandUpgradePurchaser.TryPurchase(
            FarmhandUpgradeKind.Speed2,
            state,
            availableGold: 34999);

        Assert.Equal(FarmhandUpgradePurchaseStatus.InsufficientFunds, result.Status);
        Assert.Equal(state, result.State);
        Assert.Equal(34999, result.RemainingGold);
    }

    [Fact]
    public void Catalog_UsesRequestedPrices()
    {
        Assert.Equal(15000, FarmhandUpgradeCatalog.Get(FarmhandUpgradeKind.Speed).Price);
        Assert.Equal(35000, FarmhandUpgradeCatalog.Get(FarmhandUpgradeKind.Speed2).Price);
        Assert.Equal(10000, FarmhandUpgradeCatalog.Get(FarmhandUpgradeKind.Energy).Price);
        Assert.Equal(
            FarmhandUpgradeKind.Speed,
            FarmhandUpgradeCatalog.Get(FarmhandUpgradeKind.Speed2).Prerequisite);
    }
}
