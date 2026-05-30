using Dayswork.Core.Domain;

namespace Dayswork.Core.Compat;

/// <summary>
/// Stardew Valley Expanded compatibility profile. This type is the single home for all SVE
/// identifiers (NFR-SVE-07). In U-SVE-01 the override tables are intentionally empty (BR-SVE-07):
/// every lookup reports "no override", so behavior is unchanged even with SVE installed. The
/// entrance, content, work-location, and premium-tier tables are populated by U-SVE-02..04.
/// </summary>
public sealed class SveExpansionProfile : IExpansionProfile
{
    /// <summary>SVE Content Patcher pack — the canonical "SVE content present" signal.</summary>
    public const string ContentModId = "FlashShifter.StardewValleyExpandedCP";

    /// <summary>SVE core C# mod.</summary>
    public const string CodeModId = "FlashShifter.SVECode";

    /// <summary>Immersive Farm 2 Remastered farm-map pack.</summary>
    public const string ImmersiveFarm2ModId = "flashshifter.immersivefarm2remastered";

    /// <summary>Grandpa's Farm farm-map pack.</summary>
    public const string GrandpasFarmModId = "flashshifter.GrandpasFarm";

    /// <summary>Frontier Farm farm-map pack.</summary>
    public const string FrontierFarmModId = "flashshifter.FrontierFarm";

    public string Id => "sve";

    public IReadOnlySet<string> FarmMapModIds { get; } = new HashSet<string>
    {
        ImmersiveFarm2ModId,
        GrandpasFarmModId,
        FrontierFarmModId,
    };

    public bool Matches(IReadOnlySet<string> installedModIds) =>
        installedModIds.Contains(ContentModId) || installedModIds.Contains(CodeModId);

    // ── Farm-map signatures (dimensions verified from SVE map source) ────────────
    // IF2R map is 163x156; Frontier Farm 156x65 (vanilla Standard Farm is 80x65).
    // Grandpa's Farm is 140x93 (verified from GrandpasFarm.tbin — all 18 layers).

    /// <summary>
    /// Verified per-map worker-entrance overrides, keyed by <see cref="FarmMapSignature"/>.
    /// An entry is added ONLY when manual SVE playtest confirms the existing <c>Farm.warps</c>
    /// heuristic lands the worker at the wrong tile; a farm whose heuristic already works needs no
    /// entry and falls through to the heuristic (FR-SVE-06 / BR-SVE2-04). The configured tile is a
    /// preferred spawn point — if it is blocked at spawn time the caller searches outward for the
    /// nearest passable tile (see <c>ShiftOrchestrator.FindFarmExitTile</c>).
    /// Grandpa's Farm (140x93): worker spawns at (112,51), confirmed by playtest.
    /// Frontier Farm (156x65): worker spawns at (142,16), confirmed by playtest.
    /// </summary>
    private static readonly IReadOnlyDictionary<FarmMapSignature, TileCoord> EntranceOverrides =
        new Dictionary<FarmMapSignature, TileCoord>
        {
            [new FarmMapSignature(140, 93)] = new TileCoord(112, 51),
            [new FarmMapSignature(156, 65)] = new TileCoord(142, 16),
        };

    public bool TryGetEntranceOverride(FarmMapSignature signature, out TileCoord tile) =>
        EntranceOverrides.TryGetValue(signature, out tile);

    public bool TryClassifyContentOverride(ContentDescriptor descriptor, out WorkClassification result)
    {
        result = WorkClassification.NoOverride;
        return false;
    }

    public bool IsExpansionWorkLocation(string locationName) => false;

    public AnimalBuildingTier? MapPremiumBuildingTier(string buildingType) => null;
}
