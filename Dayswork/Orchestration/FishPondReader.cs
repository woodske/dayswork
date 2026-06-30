using Dayswork.Core.Domain;
using Dayswork.Core.Shifts;
using StardewValley;
using StardewValley.Buildings;

namespace Dayswork.Orchestration;

/// <summary>
/// Live-world adapter for fish ponds (<see cref="FishPond"/> buildings). Enumerates and resolves
/// ponds, reports whether a pond has output ready to collect, and computes walkable stand tiles
/// around a pond's multi-tile footprint. Performs no mutation.
///
/// Fish ponds are <c>Building</c>s (in <c>location.buildings</c>), not <c>Data/Machines</c> objects,
/// so identity is purely <c>(location, tileX/tileY)</c> — there is no qualified item id. Collect-only:
/// the player stocks the fish and supplies any capacity-quest item. See <c>docs/machines.md</c> →
/// "Fish ponds" for the verified API.
/// </summary>
internal sealed class FishPondReader
{
    /// <summary>Every fish pond in the location, with its top-left footprint tile.</summary>
    public IEnumerable<(TileCoord Tile, FishPond Pond)> EnumerateFishPonds(GameLocation location)
    {
        foreach (var building in location.buildings)
        {
            if (building is FishPond pond && pond.daysOfConstructionLeft.Value <= 0)
                yield return (new TileCoord(pond.tileX.Value, pond.tileY.Value), pond);
        }
    }

    /// <summary>
    /// Re-resolve a selected pond against the live world by its top-left tile. Returns null when no
    /// fully-built fish pond occupies that tile anymore (moved/demolished) — skip with a dev log,
    /// same contract as machine tiles.
    /// </summary>
    public FishPond? Resolve(GameLocation location, TileCoord tile)
    {
        foreach (var building in location.buildings)
        {
            if (building is FishPond pond
                && pond.daysOfConstructionLeft.Value <= 0
                && pond.tileX.Value == tile.X
                && pond.tileY.Value == tile.Y)
            {
                return pond;
            }
        }

        return null;
    }

    /// <summary>True when the pond is holding finished produce waiting to be collected.</summary>
    public static bool HasOutput(FishPond pond) => pond.output.Value is not null;

    /// <summary>
    /// Walkable stand tiles immediately around the pond's footprint, nearest the visible output
    /// bucket first (the bucket sits at the pond's bottom-right via <c>GetItemBucketTile</c>), so the
    /// worker tends to approach the side where the output appears. The caller picks the nearest
    /// reachable one via the route selector.
    /// </summary>
    public static IEnumerable<StandTile> StandTilesAround(FishPond pond)
    {
        int left = pond.tileX.Value;
        int top = pond.tileY.Value;
        int width = Math.Max(1, pond.tilesWide.Value);
        int height = Math.Max(1, pond.tilesHigh.Value);
        int right = left + width - 1;
        int bottom = top + height - 1;

        var ring = new List<StandTile>();

        // Bottom edge (where the output bucket renders) first, then right, top, left.
        for (int x = left; x <= right; x++)
            ring.Add(new StandTile(new TileCoord(x, bottom + 1), false));
        for (int y = top; y <= bottom; y++)
            ring.Add(new StandTile(new TileCoord(right + 1, y), false));
        for (int x = left; x <= right; x++)
            ring.Add(new StandTile(new TileCoord(x, top - 1), false));
        for (int y = top; y <= bottom; y++)
            ring.Add(new StandTile(new TileCoord(left - 1, y), false));

        // Diagonal footprint corners last: the player can collect from a diagonal too, but the
        // selector adds a travel penalty, so corners are only used when closer/only-option.
        ring.Add(new StandTile(new TileCoord(left - 1, top - 1), true));
        ring.Add(new StandTile(new TileCoord(right + 1, top - 1), true));
        ring.Add(new StandTile(new TileCoord(left - 1, bottom + 1), true));
        ring.Add(new StandTile(new TileCoord(right + 1, bottom + 1), true));

        return ring;
    }
}
