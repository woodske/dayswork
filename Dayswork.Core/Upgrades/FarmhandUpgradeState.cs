namespace Dayswork.Core.Upgrades;

public sealed record FarmhandUpgradeState(bool SpeedPurchased, bool EnergyPurchased, bool Speed2Purchased = false)
{
    public static readonly FarmhandUpgradeState Empty = new(false, false, false);

    public bool IsPurchased(FarmhandUpgradeKind kind) =>
        kind switch
        {
            FarmhandUpgradeKind.Speed => SpeedPurchased,
            FarmhandUpgradeKind.Speed2 => Speed2Purchased,
            FarmhandUpgradeKind.Energy => EnergyPurchased,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };

    public FarmhandUpgradeState MarkPurchased(FarmhandUpgradeKind kind) =>
        kind switch
        {
            FarmhandUpgradeKind.Speed => this with { SpeedPurchased = true },
            FarmhandUpgradeKind.Speed2 => this with { Speed2Purchased = true },
            FarmhandUpgradeKind.Energy => this with { EnergyPurchased = true },
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
}
