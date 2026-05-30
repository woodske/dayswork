using Dayswork.Core.Compat;
using Dayswork.Core.Domain;
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
}
