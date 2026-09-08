namespace Dayswork.Core.Persistence.Dto;

/// <summary>
/// The upgrade save envelope. Schemas 1 and 2 stored one global set of flags on this object itself
/// (v2 adding <see cref="Speed2Purchased"/>); schema 3 moves them into <see cref="Owners"/>, keyed
/// by <c>Farmer.UniqueMultiplayerID</c>, and leaves the flat fields unset. Reading keeps both
/// shapes so a v1/v2 save migrates onto the host's id (2.0 plan D9).
/// </summary>
public sealed class FarmhandUpgradeSaveDataV1
{
    public int SchemaVersion { get; set; } = 1;

    // ── Schema 1/2 (global) ──────────────────────────────────────────────────
    public bool SpeedPurchased { get; set; }
    public bool EnergyPurchased { get; set; }
    public bool Speed2Purchased { get; set; }

    // ── Schema 3 (per owner) ─────────────────────────────────────────────────
    /// <summary>Owner multiplayer id (as a string, because JSON object keys are strings) → that
    /// player's purchases.</summary>
    public Dictionary<string, FarmhandUpgradeOwnerDto>? Owners { get; set; }
}

public sealed class FarmhandUpgradeOwnerDto
{
    public bool SpeedPurchased { get; set; }
    public bool EnergyPurchased { get; set; }
    public bool Speed2Purchased { get; set; }
}
