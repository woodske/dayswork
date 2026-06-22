namespace Dayswork.Core.Upgrades;

public static class FarmhandUpgradeCatalog
{
    public static readonly FarmhandUpgradeDefinition Speed = new(
        FarmhandUpgradeKind.Speed,
        Price: 15000,
        SpeedBonus: 1,
        EnergyBonus: 0);

    public static readonly FarmhandUpgradeDefinition Energy = new(
        FarmhandUpgradeKind.Energy,
        Price: 10000,
        SpeedBonus: 0,
        EnergyBonus: 50);

    public static IReadOnlyList<FarmhandUpgradeDefinition> All { get; } =
        new[] { Speed, Energy };

    public static FarmhandUpgradeDefinition Get(FarmhandUpgradeKind kind) =>
        kind switch
        {
            FarmhandUpgradeKind.Speed => Speed,
            FarmhandUpgradeKind.Energy => Energy,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
}
