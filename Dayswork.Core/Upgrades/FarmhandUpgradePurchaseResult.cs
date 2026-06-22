namespace Dayswork.Core.Upgrades;

public enum FarmhandUpgradePurchaseStatus
{
    Purchased,
    AlreadyPurchased,
    InsufficientFunds,
}

public sealed record FarmhandUpgradePurchaseResult(
    FarmhandUpgradePurchaseStatus Status,
    FarmhandUpgradeState State,
    int RemainingGold,
    int Price);
