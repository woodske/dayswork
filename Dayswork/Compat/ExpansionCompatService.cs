using Dayswork.Core.Compat;
using Dayswork.Core.Domain;
using Dayswork.Worker;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Buildings;

namespace Dayswork.Compat;

/// <summary>
/// The single runtime seam consumers use for expansion compatibility. Holds the active
/// <see cref="IExpansionProfile"/> (resolved once at startup) plus the pure
/// <see cref="AnimalBuildingCapacityPolicy"/>, and applies them to live game objects. When the
/// active profile is the Vanilla Null-Object (or, in U-SVE-01, the SVE profile with empty tables),
/// every operation is a passthrough/no-op, so behavior is identical to vanilla (P-SVE-02/03/04).
/// </summary>
internal sealed class ExpansionCompatService
{
    private readonly AnimalBuildingCapacityPolicy _capacityPolicy;
    private IExpansionProfile _activeProfile;

    public ExpansionCompatService(IExpansionProfile defaultProfile, AnimalBuildingCapacityPolicy capacityPolicy)
    {
        _activeProfile = defaultProfile;
        _capacityPolicy = capacityPolicy;
    }

    /// <summary>Id of the currently active profile (for diagnostics).</summary>
    public string ActiveProfileId => _activeProfile.Id;

    /// <summary>Assigns the resolved active profile (called once at GameLaunched).</summary>
    public void SetActiveProfile(IExpansionProfile profile) => _activeProfile = profile;

    /// <summary>
    /// Per-map worker-entrance override. Returns false when no override applies; the caller then
    /// keeps using the existing <c>Farm.warps</c> heuristic and fallback tile.
    /// </summary>
    public bool TryGetFarmEntranceOverride(GameLocation farm, out Point tile)
    {
        if (TryComputeSignature(farm, out var signature) &&
            _activeProfile.TryGetEntranceOverride(signature, out var coord))
        {
            tile = new Point(coord.X, coord.Y);
            return true;
        }

        tile = Point.Zero;
        return false;
    }

    /// <summary>
    /// Computes the farm-map signature from the live map (dimensions; an optional discriminator is
    /// reserved for maps that share dimensions). Guarded so a missing/odd map never throws — the
    /// caller then falls back to the warp heuristic (NFRU2-02).
    /// </summary>
    private static bool TryComputeSignature(GameLocation farm, out FarmMapSignature signature)
    {
        try
        {
            var map = farm.Map;
            if (map is null || map.Layers.Count == 0)
            {
                signature = default;
                return false;
            }

            var layer = map.Layers[0];
            signature = new FarmMapSignature(layer.LayerWidth, layer.LayerHeight);
            return true;
        }
        catch
        {
            signature = default;
            return false;
        }
    }

    /// <summary>
    /// Derives the animal house's feed capacity from its real trough tiles. The MaxOccupants upper
    /// bound is wired in U-SVE-03 (where vanilla parity is verified against building data); until
    /// then the trough count is authoritative.
    /// </summary>
    public int ResolveAnimalFeedCapacity(AnimalHouse house)
    {
        var troughs = CountTroughTiles(house);

        // Bound by the building's real max occupants (= 16 for SVE premium). When the parent building
        // or its occupant cap is unavailable, fall back to the trough count so capacity is unchanged
        // from the U-SVE-01 behavior and never over-fills.
        var maxOccupants = house.ParentBuilding?.maxOccupants.Value ?? 0;
        if (maxOccupants <= 0)
            maxOccupants = troughs;

        return _capacityPolicy.DeriveCapacity(new AnimalBuildingCapacityInputs(troughs, maxOccupants));
    }

    /// <summary>
    /// Premium animal-building tier mapping keyed on the raw building type string (as carried by the
    /// hiring enumeration on <c>BuildingOutline</c>). Returns false when the active profile has no
    /// mapping — i.e., vanilla buildings, which then keep the existing vanilla tier inference.
    /// </summary>
    public bool TryResolvePremiumBuildingTier(string buildingType, out AnimalBuildingTier tier)
    {
        if (_activeProfile.MapPremiumBuildingTier(buildingType) is { } mapped)
        {
            tier = mapped;
            return true;
        }

        tier = default;
        return false;
    }

    /// <summary>
    /// Maps a live animal building to the vanilla tier used for scope/pricing. Returns the supplied
    /// vanilla tier unchanged when the active profile has no mapping (e.g., vanilla buildings).
    /// </summary>
    public AnimalBuildingTier ResolveAnimalBuildingTier(Building building, AnimalBuildingTier vanillaTier) =>
        _activeProfile.MapPremiumBuildingTier(building.buildingType.Value) ?? vanillaTier;

    /// <summary>
    /// Content-classification override hook. Descriptor construction from live world objects is
    /// implemented in U-SVE-04; until then no override applies.
    /// </summary>
    public bool TryClassifyContentOverride(GameLocation location, TileCoord tile, out WorkClassification result)
    {
        result = WorkClassification.NoOverride;
        return false;
    }

    /// <summary>Whether the location is an expansion work location (e.g., Grandpa's Shed).</summary>
    public bool IsExpansionWorkLocation(GameLocation location) =>
        _activeProfile.IsExpansionWorkLocation(location.NameOrUniqueName);

    public bool TryGetExpansionLocationDescriptor(string locationName, out ExpansionLocationDescriptor descriptor)
    {
        descriptor = _activeProfile.GetLocationDescriptors().FirstOrDefault(candidate =>
            string.Equals(candidate.LocationName, locationName, StringComparison.OrdinalIgnoreCase))!;
        return descriptor is not null;
    }

    public IReadOnlyList<ExpansionLocationDescriptor> GetExpansionLocationDescriptors() =>
        _activeProfile.GetLocationDescriptors();

    public bool IsExpansionDepositLocation(string locationName) =>
        TryGetExpansionLocationDescriptor(locationName, out var descriptor) &&
        descriptor.IsDepositDestinationEligible;

    public bool IsExpansionChestVisibleForScope(
        ExpansionLocationDescriptor descriptor,
        GreenhouseSelection? selectedGreenhouse) =>
        descriptor.IsDepositDestinationEligible &&
        selectedGreenhouse is not null &&
        string.Equals(
            selectedGreenhouse.LocationName,
            descriptor.AssociatedWorkLocationName,
            StringComparison.OrdinalIgnoreCase);

    public bool TryValidateRoute(
        Farm farm,
        string sourceLocationName,
        string targetLocationName,
        ExpansionRoutePurpose purpose,
        out ValidatedExpansionRoute route,
        out ExpansionRouteFailure failure)
    {
        if (!TryComputeSignature(farm, out var signature))
        {
            var requestWithoutSignature = new ExpansionRouteRequest(
                new FarmMapSignature(0, 0),
                sourceLocationName,
                targetLocationName,
                purpose);
            route = null!;
            failure = new ExpansionRouteFailure(
                requestWithoutSignature,
                null,
                null,
                ExpansionRouteFailureReason.UnsupportedFarmSignature,
                "farm_signature_unavailable");
            return false;
        }

        var request = new ExpansionRouteRequest(signature, sourceLocationName, targetLocationName, purpose);
        if (!_activeProfile.TryGetRoute(request, out var definition))
        {
            route = null!;
            failure = new ExpansionRouteFailure(
                request,
                null,
                null,
                ExpansionRouteFailureReason.RouteNotDefined,
                "route_not_defined");
            return false;
        }

        var hops = new List<ValidatedExpansionRouteHop>(definition.Hops.Count);
        foreach (var hop in definition.Hops.OrderBy(hop => hop.Ordinal))
        {
            if (!TryResolveLocation(farm, hop.SourceLocationName, out var source))
            {
                route = null!;
                failure = new ExpansionRouteFailure(
                    request,
                    definition.Id,
                    hop.Ordinal,
                    ExpansionRouteFailureReason.SourceLocationMissing,
                    $"source_missing_{hop.SourceLocationName}");
                return false;
            }

            if (!TryResolveLocation(farm, hop.TargetLocationName, out var target))
            {
                route = null!;
                failure = new ExpansionRouteFailure(
                    request,
                    definition.Id,
                    hop.Ordinal,
                    ExpansionRouteFailureReason.TargetLocationMissing,
                    $"target_missing_{hop.TargetLocationName}");
                return false;
            }

            if (!IsWithinMap(hop.ApproachTile, source))
            {
                route = null!;
                failure = new ExpansionRouteFailure(
                    request,
                    definition.Id,
                    hop.Ordinal,
                    ExpansionRouteFailureReason.ApproachTileInvalid,
                    $"approach_invalid_{source.NameOrUniqueName}_{hop.ApproachTile.X}_{hop.ApproachTile.Y}");
                return false;
            }

            if (!IsWithinMap(hop.ArrivalTile, target))
            {
                route = null!;
                failure = new ExpansionRouteFailure(
                    request,
                    definition.Id,
                    hop.Ordinal,
                    ExpansionRouteFailureReason.ArrivalTileInvalid,
                    $"arrival_invalid_{target.NameOrUniqueName}_{hop.ArrivalTile.X}_{hop.ArrivalTile.Y}");
                return false;
            }

            if (!WorkerMovementDriver.IsTilePassableForWorker(new Point(hop.ApproachTile.X, hop.ApproachTile.Y), source))
            {
                route = null!;
                failure = new ExpansionRouteFailure(
                    request,
                    definition.Id,
                    hop.Ordinal,
                    ExpansionRouteFailureReason.ApproachTileBlocked,
                    $"approach_blocked_{source.NameOrUniqueName}_{hop.ApproachTile.X}_{hop.ApproachTile.Y}");
                return false;
            }

            if (!WorkerMovementDriver.IsTilePassableForWorker(new Point(hop.ArrivalTile.X, hop.ArrivalTile.Y), target))
            {
                route = null!;
                failure = new ExpansionRouteFailure(
                    request,
                    definition.Id,
                    hop.Ordinal,
                    ExpansionRouteFailureReason.ArrivalTileBlocked,
                    $"arrival_blocked_{target.NameOrUniqueName}_{hop.ArrivalTile.X}_{hop.ArrivalTile.Y}");
                return false;
            }

            hops.Add(new ValidatedExpansionRouteHop(hop, source, target));
        }

        route = new ValidatedExpansionRoute(definition, hops);
        failure = null!;
        return true;
    }

    internal static string FormatRouteFailure(ExpansionRouteFailure failure)
    {
        var route = failure.RouteId?.Value ?? "<unresolved>";
        var hop = failure.HopOrdinal is null ? "n/a" : failure.HopOrdinal.Value.ToString();
        return $"expansion_route|route={route}|purpose={failure.Request.Purpose}|" +
               $"source={failure.Request.SourceLocationName}|target={failure.Request.TargetLocationName}|" +
               $"hop={hop}|reason={failure.Reason}|detail={failure.Detail}";
    }

    private static int CountTroughTiles(GameLocation location)
    {
        var layer = location.Map.Layers[0];
        var count = 0;
        for (var x = 0; x < layer.LayerWidth; x++)
        for (var y = 0; y < layer.LayerHeight; y++)
        {
            if (location.doesTileHaveProperty(x, y, "Trough", "Back", false) is not null)
                count++;
        }

        return count;
    }

    private static bool TryResolveLocation(Farm farm, string locationName, out GameLocation location)
    {
        if (string.Equals(locationName, "Farm", StringComparison.OrdinalIgnoreCase))
        {
            location = farm;
            return true;
        }

        location = Game1.getLocationFromName(locationName);
        return location is not null;
    }

    private static bool IsWithinMap(TileCoord tile, GameLocation location)
    {
        var layer = location.Map.Layers[0];
        return tile.X >= 0 &&
               tile.Y >= 0 &&
               tile.X < layer.LayerWidth &&
               tile.Y < layer.LayerHeight;
    }
}

internal sealed record ValidatedExpansionRoute(
    ExpansionRouteDefinition Definition,
    IReadOnlyList<ValidatedExpansionRouteHop> Hops);

internal sealed record ValidatedExpansionRouteHop(
    ExpansionRouteHop Hop,
    GameLocation Source,
    GameLocation Target);
