namespace Dayswork.Core.Upgrades;

public sealed record FarmhandUpgradeDefinition(
    FarmhandUpgradeKind Kind,
    int Price,
    int SpeedBonus,
    int EnergyBonus,
    FarmhandUpgradeKind? Prerequisite = null);
