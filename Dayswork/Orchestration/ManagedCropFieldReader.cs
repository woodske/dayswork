using Dayswork.Core.Crops;
using Dayswork.Core.Domain;
using Dayswork.Worker;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.TerrainFeatures;

namespace Dayswork.Orchestration;

/// <summary>
/// Thin live-world → pure adapter. Snapshots the managed-crop zone tiles of a live
/// <see cref="GameLocation"/> into a pure <see cref="FieldState"/> the <see cref="CropShiftPlanner"/>
/// consumes. Performs no mutation.
/// </summary>
internal sealed class ManagedCropFieldReader
{
    public FieldState Read(
        GameLocation location,
        GameDate date,
        IReadOnlyList<CropZoneAssignment> assignments,
        bool isSeasonAgnosticLocation)
    {
        var locationName = location.NameOrUniqueName;
        var seen = new HashSet<TileCoord>();
        var tiles = new List<TileState>();

        foreach (var assignment in assignments)
        {
            var zone = assignment.Zone;
            if (!string.Equals(zone.LocationName, locationName, StringComparison.Ordinal))
                continue;

            for (var x = zone.TopLeft.X; x <= zone.BottomRight.X; x++)
            for (var y = zone.TopLeft.Y; y <= zone.BottomRight.Y; y++)
            {
                var coord = new TileCoord(x, y);
                if (!seen.Add(coord))
                    continue;

                if (TryReadTile(location, coord, out var state))
                    tiles.Add(state);
            }
        }

        return new FieldState(locationName, date, isSeasonAgnosticLocation, tiles);
    }

    /// <summary>
    /// True when <paramref name="tileVec"/> holds a non-debris Object that should be collected as
    /// produce (e.g. forage left by a wild-seed crop's replaceWithObjectOnFullGrown). Excludes big
    /// craftables, weeds, twigs, and rocks/ores.
    /// </summary>
    internal static bool IsProduceObjectOnCropTile(Vector2 tileVec, GameLocation loc)
    {
        if (!loc.objects.TryGetValue(tileVec, out var obj))
            return false;
        if (obj.bigCraftable.Value) return false;
        if (obj.IsWeeds()) return false;
        if (obj.Name == "Twig") return false;
        if (ObjectTargetClassifier.ClassifyPick(tileVec, loc) is not null) return false;
        return true;
    }

    /// <summary>True when a sprinkler occupies <paramref name="vec"/> (sprinklers live in
    /// <see cref="GameLocation.objects"/>; <c>IsSprinkler()</c> is base-radius driven).</summary>
    internal static bool HasSprinkler(GameLocation location, Vector2 vec) =>
        location.objects.TryGetValue(vec, out var obj) && obj.IsSprinkler();

    /// <summary>
    /// True when the tile is a tillable/plantable managed-crop candidate: existing <see cref="HoeDirt"/>
    /// or bare ground the live map marks <c>Diggable</c> — and <b>not</b> occupied by a sprinkler. A
    /// stone/weed on diggable ground still counts (it is plantable once cleared). Used by the draw-zone
    /// UI's valid-tile count so it mirrors what the worker will actually work.
    /// </summary>
    internal static bool IsPlantableGroundTile(GameLocation location, int x, int y)
    {
        var vec = new Vector2(x, y);
        if (HasSprinkler(location, vec))
            return false;
        if (location.terrainFeatures.TryGetValue(vec, out var tf) && tf is HoeDirt)
            return true;
        return location.doesTileHaveProperty(x, y, "Diggable", "Back") is not null;
    }

    /// <summary>
    /// Counts the distinct tillable/plantable tiles across <paramref name="zones"/> using the live map
    /// for each zone's location — excluding sprinklers and non-diggable ground. Single source of truth
    /// for both the Draw-Zones overlay count and the crop-group summary so they always agree.
    /// </summary>
    internal static int CountPlantableTiles(IEnumerable<Zone> zones)
    {
        var seen = new HashSet<(string, int, int)>();
        var count = 0;
        foreach (var zone in zones)
        {
            var location = ResolveLocation(zone.LocationName);
            if (location is null)
                continue;
            for (var x = zone.TopLeft.X; x <= zone.BottomRight.X; x++)
            for (var y = zone.TopLeft.Y; y <= zone.BottomRight.Y; y++)
            {
                if (seen.Add((zone.LocationName, x, y)) && IsPlantableGroundTile(location, x, y))
                    count++;
            }
        }
        return count;
    }

    private static GameLocation? ResolveLocation(string locationName) =>
        string.Equals(locationName, "Farm", StringComparison.Ordinal)
            ? Game1.getFarm()
            : Game1.getLocationFromName(locationName);

    private static bool TryReadTile(GameLocation location, TileCoord coord, out TileState state)
    {
        var vec = new Vector2(coord.X, coord.Y);
        var diggable = location.doesTileHaveProperty(coord.X, coord.Y, "Diggable", "Back") is not null;

        // A sprinkler is the player's own fixture — never till, plant on, or clear it. Skipping the
        // tile entirely keeps it out of FieldState so no action is queued on it.
        if (HasSprinkler(location, vec))
        {
            state = default!;
            return false;
        }

        if (location.terrainFeatures.TryGetValue(vec, out var tf) && tf is HoeDirt dirt)
        {
            var crop = dirt.crop;
            var isDead = crop is not null && crop.dead.Value;
            var hasLiveCrop = crop is not null && !isDead;
            // Wild seed auto-harvest places forage in loc.objects and removes dirt.crop.
            // Treat it as a harvestable crop so the planner queues Harvest, not PlantSeed.
            var hasWildSeedForage = !hasLiveCrop && !isDead && IsProduceObjectOnCropTile(vec, location);
            state = new TileState(
                Tile: coord,
                ReadyToHarvest: (hasLiveCrop && dirt.readyForHarvest()) || hasWildSeedForage,
                HasCrop: hasLiveCrop || hasWildSeedForage,
                HasDebris: isDead,                                  // dead crop is cleared as debris
                IsTilled: true,
                HasFertilizer: !string.IsNullOrEmpty(dirt.fertilizer.Value) && dirt.fertilizer.Value != "0",
                IsWatered: dirt.state.Value == HoeDirt.watered);
            return true;
        }

        // Non-tilled tile occupied by a clearable terrain feature (grass/tree), object
        // (stone/twig/weeds), or resource clump (hardwood stump, boulder) is debris.
        // Resource clumps live in location.resourceClumps — not terrainFeatures or objects.
        var hasBlockingFeature = location.terrainFeatures.ContainsKey(vec)
            || location.objects.ContainsKey(vec)
            || ObjectTargetClassifier.FindResourceClumpAt(vec, location) is not null;
        if (hasBlockingFeature)
        {
            state = new TileState(
                Tile: coord,
                ReadyToHarvest: false,
                HasCrop: false,
                HasDebris: true,
                IsTilled: false,
                HasFertilizer: false,
                IsWatered: false);
            return true;
        }

        // Bare ground: only a candidate for till/plant when the live map marks it Diggable.
        // Non-diggable empty tiles (walls, paths, fixtures inside the drawn rectangle) are skipped.
        if (!diggable)
        {
            state = default!;
            return false;
        }

        state = new TileState(
            Tile: coord,
            ReadyToHarvest: false,
            HasCrop: false,
            HasDebris: false,
            IsTilled: false,
            HasFertilizer: false,
            IsWatered: false);
        return true;
    }
}
