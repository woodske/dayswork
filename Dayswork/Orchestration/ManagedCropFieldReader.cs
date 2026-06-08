using Dayswork.Core.Crops;
using Dayswork.Core.Domain;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.TerrainFeatures;

namespace Dayswork.Orchestration;

/// <summary>
/// Thin live-world → pure adapter (U-MC-05). Snapshots the managed-crop zone tiles of a live
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

    private static bool TryReadTile(GameLocation location, TileCoord coord, out TileState state)
    {
        var vec = new Vector2(coord.X, coord.Y);
        var diggable = location.doesTileHaveProperty(coord.X, coord.Y, "Diggable", "Back") is not null;

        if (location.terrainFeatures.TryGetValue(vec, out var tf) && tf is HoeDirt dirt)
        {
            var crop = dirt.crop;
            var isDead = crop is not null && crop.dead.Value;
            var hasLiveCrop = crop is not null && !isDead;
            state = new TileState(
                Tile: coord,
                ReadyToHarvest: hasLiveCrop && dirt.readyForHarvest(),
                HasCrop: hasLiveCrop,
                HasDebris: isDead,                                  // dead crop is cleared as debris
                IsTilled: true,
                HasFertilizer: !string.IsNullOrEmpty(dirt.fertilizer.Value) && dirt.fertilizer.Value != "0",
                IsWatered: dirt.state.Value == HoeDirt.watered);
            return true;
        }

        // Non-tilled tile occupied by a clearable terrain feature (grass/tree) or object
        // (stone/twig/weeds) is debris that blocks tilling.
        var hasBlockingFeature = location.terrainFeatures.ContainsKey(vec) || location.objects.ContainsKey(vec);
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
