namespace Dayswork.Core.Upgrades;

public sealed record FarmhandUpgradeState(bool SpeedPurchased, bool EnergyPurchased)
{
    public static readonly FarmhandUpgradeState Empty = new(false, false);

    public bool IsPurchased(FarmhandUpgradeKind kind) =>
        kind switch
        {
            FarmhandUpgradeKind.Speed => SpeedPurchased,
            FarmhandUpgradeKind.Energy => EnergyPurchased,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };

    public FarmhandUpgradeState MarkPurchased(FarmhandUpgradeKind kind) =>
        kind switch
        {
            FarmhandUpgradeKind.Speed => this with { SpeedPurchased = true },
            FarmhandUpgradeKind.Energy => this with { EnergyPurchased = true },
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
}
