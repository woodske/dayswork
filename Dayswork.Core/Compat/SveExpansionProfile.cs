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

    /// <summary>SVE Premium Coop building type (16 occupants; upgrade above Deluxe Coop).</summary>
    public const string PremiumCoopBuildingType = "FlashShifter.StardewValleyExpandedCP_PremiumCoop";

    /// <summary>SVE Premium Barn building type (16 occupants; upgrade above Deluxe Barn).</summary>
    public const string PremiumBarnBuildingType = "FlashShifter.StardewValleyExpandedCP_PremiumBarn";

    public const string GrandpasShedLocation = "Custom_GrandpasShed";

    public const string GrandpasShedGreenhouseLocation = "Custom_GrandpasShedGreenhouse";

    private const string GrandpasShedDisplayName = "Grandpa's Shed";

    private const string GrandpasShedGreenhouseDisplayName = "Grandpa's Shed Greenhouse";

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

    private static readonly FarmMapSignature GrandpasFarmSignature = new(140, 93);
    private static readonly FarmMapSignature ImmersiveFarm2Signature = new(163, 156);
    private static readonly FarmMapSignature FrontierFarmSignature = new(156, 65);

    // Source-grounded SVE route tiles, verified from the local SVE repository:
    // - GrandpasFarm_ShedFixed.tbin patched to Farm (15,11): farm actions (20,21)/(21,21),
    //   shed return LoadMap Farm (20,22)/(21,22).
    // - FarmShedFixed.tbin patched to Farm (138,23): farm actions (144,33)/(145,33),
    //   shed return LoadMap Farm (144,34)/(145,34).
    // - FrontierFarm_ShedFixed.tmx patched to Farm (14,11): farm actions (18,21)/(19,21),
    //   shed return LoadMap Farm (18,22)/(19,22).
    // - GrandpasShed.tbin: shed action (25,13) -> Custom_GrandpasShedGreenhouse (30,16).
    // - GrandpasShedGreenhouse.tbin: greenhouse action (30,15) -> Custom_GrandpasShed (25,14).
    private static readonly TileCoord ShedArrivalTile = new(15, 16);
    private static readonly TileCoord ShedGreenhouseApproachTile = new(25, 14);
    private static readonly TileCoord ShedExitApproachTile = new(15, 17);
    private static readonly TileCoord GreenhouseArrivalTile = new(30, 16);
    private static readonly TileCoord GreenhouseExitApproachTile = new(30, 16);
    private static readonly TileCoord ShedFromGreenhouseArrivalTile = new(25, 14);

    private static readonly IReadOnlyList<ExpansionLocationDescriptor> LocationDescriptors =
        new[]
        {
            new ExpansionLocationDescriptor(
                GrandpasShedGreenhouseLocation,
                GrandpasShedGreenhouseDisplayName,
                ExpansionLocationRole.GreenhouseWork,
                GrandpasShedGreenhouseLocation),
            new ExpansionLocationDescriptor(
                GrandpasShedLocation,
                GrandpasShedDisplayName,
                ExpansionLocationRole.DepositOnly,
                GrandpasShedGreenhouseLocation),
        };

    private static readonly IReadOnlyList<ExpansionRouteDefinition> Routes = BuildRoutes();

    public bool TryGetEntranceOverride(FarmMapSignature signature, out TileCoord tile) =>
        EntranceOverrides.TryGetValue(signature, out tile);

    public bool TryClassifyContentOverride(ContentDescriptor descriptor, out WorkClassification result)
    {
        result = WorkClassification.NoOverride;
        return false;
    }

    public bool IsExpansionWorkLocation(string locationName) =>
        LocationDescriptors.Any(descriptor =>
            string.Equals(descriptor.LocationName, locationName, StringComparison.OrdinalIgnoreCase));

    public IReadOnlyList<ExpansionLocationDescriptor> GetLocationDescriptors() => LocationDescriptors;

    public bool TryGetRoute(ExpansionRouteRequest request, out ExpansionRouteDefinition route)
    {
        route = Routes.FirstOrDefault(candidate =>
            candidate.FarmSignature == request.FarmSignature &&
            candidate.Purpose == request.Purpose &&
            string.Equals(candidate.SourceLocationName, request.SourceLocationName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(candidate.TargetLocationName, request.TargetLocationName, StringComparison.OrdinalIgnoreCase))!;

        return route is not null;
    }

    // SVE Premium Coop/Barn are upgrades above Deluxe (MaxOccupants 16). They have no dedicated
    // Dayswork tier; per App Design Q4=A they map to the nearest vanilla tier (Deluxe) for
    // scope/pricing, with no enum or save-schema change. Every other building type → null
    // (pass-through to the vanilla tier inference). All SVE ids stay in this profile (NFR-SVE-07).
    public AnimalBuildingTier? MapPremiumBuildingTier(string buildingType) => buildingType switch
    {
        PremiumCoopBuildingType => AnimalBuildingTier.DeluxeCoop,
        PremiumBarnBuildingType => AnimalBuildingTier.DeluxeBarn,
        _ => null,
    };

    private static IReadOnlyList<ExpansionRouteDefinition> BuildRoutes()
    {
        var routes = new List<ExpansionRouteDefinition>();
        AddFarmRoutes(routes, "grandpas-farm", GrandpasFarmSignature, new TileCoord(20, 22));
        AddFarmRoutes(routes, "if2r", ImmersiveFarm2Signature, new TileCoord(144, 34));
        AddFarmRoutes(routes, "frontier", FrontierFarmSignature, new TileCoord(18, 22));
        return routes;
    }

    private static void AddFarmRoutes(
        List<ExpansionRouteDefinition> routes,
        string farmId,
        FarmMapSignature signature,
        TileCoord farmShedApproachTile)
    {
        var farmToShed = new ExpansionRouteHop(
            1,
            "Farm",
            farmShedApproachTile,
            GrandpasShedLocation,
            ShedArrivalTile);

        var shedToGreenhouse = new ExpansionRouteHop(
            2,
            GrandpasShedLocation,
            ShedGreenhouseApproachTile,
            GrandpasShedGreenhouseLocation,
            GreenhouseArrivalTile);

        var greenhouseToShed = new ExpansionRouteHop(
            1,
            GrandpasShedGreenhouseLocation,
            GreenhouseExitApproachTile,
            GrandpasShedLocation,
            ShedFromGreenhouseArrivalTile);

        var shedToFarm = new ExpansionRouteHop(
            2,
            GrandpasShedLocation,
            ShedExitApproachTile,
            "Farm",
            farmShedApproachTile);

        routes.Add(new ExpansionRouteDefinition(
            new ExpansionRouteId($"sve.{farmId}.farm-to-shed-greenhouse.work-entry"),
            signature,
            "Farm",
            GrandpasShedGreenhouseLocation,
            ExpansionRoutePurpose.WorkEntry,
            new[] { farmToShed, shedToGreenhouse }));

        routes.Add(new ExpansionRouteDefinition(
            new ExpansionRouteId($"sve.{farmId}.farm-to-shed-greenhouse.deposit-entry"),
            signature,
            "Farm",
            GrandpasShedGreenhouseLocation,
            ExpansionRoutePurpose.DepositEntry,
            new[] { farmToShed, shedToGreenhouse }));

        routes.Add(new ExpansionRouteDefinition(
            new ExpansionRouteId($"sve.{farmId}.farm-to-shed.deposit-entry"),
            signature,
            "Farm",
            GrandpasShedLocation,
            ExpansionRoutePurpose.DepositEntry,
            new[] { farmToShed }));

        routes.Add(new ExpansionRouteDefinition(
            new ExpansionRouteId($"sve.{farmId}.shed-greenhouse-to-farm.return"),
            signature,
            GrandpasShedGreenhouseLocation,
            "Farm",
            ExpansionRoutePurpose.ReturnToFarm,
            new[] { greenhouseToShed, shedToFarm }));

        routes.Add(new ExpansionRouteDefinition(
            new ExpansionRouteId($"sve.{farmId}.shed-to-farm.return"),
            signature,
            GrandpasShedLocation,
            "Farm",
            ExpansionRoutePurpose.ReturnToFarm,
            new[] { shedToFarm with { Ordinal = 1 } }));
    }
}
