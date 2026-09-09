namespace Dayswork.Core.Upgrades;

public sealed record FarmhandUpgradeState(bool SpeedPurchased, bool EnergyPurchased, bool Speed2Purchased = false)
{
    public static readonly FarmhandUpgradeState Empty = new(false, false, false);

    /// <summary>
    /// Folds a newly reported state into what was already known. Upgrades are only ever bought and
    /// never given back, so this is a union — which is what lets a client accept the host's answers
    /// in any order: a menu snapshot that was in flight while a purchase completed cannot relock
    /// what the player has just paid for (R8).
    /// </summary>
    public static FarmhandUpgradeState Merge(FarmhandUpgradeState? known, FarmhandUpgradeState incoming) =>
        known is null
            ? incoming
            : new FarmhandUpgradeState(
                known.SpeedPurchased || incoming.SpeedPurchased,
                known.EnergyPurchased || incoming.EnergyPurchased,
                known.Speed2Purchased || incoming.Speed2Purchased);

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
