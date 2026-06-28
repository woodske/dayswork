namespace Dayswork.Core.Upgrades;

public enum FarmhandUpgradePurchaseStatus
{
    Purchased,
    AlreadyPurchased,
    InsufficientFunds,
    PrerequisiteNotMet,
}

public sealed record FarmhandUpgradePurchaseResult(
    FarmhandUpgradePurchaseStatus Status,
    FarmhandUpgradeState State,
    int RemainingGold,
    int Price);
