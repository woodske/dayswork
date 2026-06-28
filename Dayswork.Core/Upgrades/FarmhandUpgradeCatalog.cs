namespace Dayswork.Core.Upgrades;

public static class FarmhandUpgradeCatalog
{
    public static readonly FarmhandUpgradeDefinition Speed = new(
        FarmhandUpgradeKind.Speed,
        Price: 15000,
        SpeedBonus: 1,
        EnergyBonus: 0);

    public static readonly FarmhandUpgradeDefinition Speed2 = new(
        FarmhandUpgradeKind.Speed2,
        Price: 35000,
        SpeedBonus: 1,
        EnergyBonus: 0,
        Prerequisite: FarmhandUpgradeKind.Speed);

    public static readonly FarmhandUpgradeDefinition Energy = new(
        FarmhandUpgradeKind.Energy,
        Price: 10000,
        SpeedBonus: 0,
        EnergyBonus: 50);

    public static IReadOnlyList<FarmhandUpgradeDefinition> All { get; } =
        new[] { Speed, Speed2, Energy };

    public static FarmhandUpgradeDefinition Get(FarmhandUpgradeKind kind) =>
        kind switch
        {
            FarmhandUpgradeKind.Speed => Speed,
            FarmhandUpgradeKind.Speed2 => Speed2,
            FarmhandUpgradeKind.Energy => Energy,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
}
