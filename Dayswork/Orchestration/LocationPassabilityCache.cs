using Dayswork.Core.Domain;
using Dayswork.Core.Pathing;
using Dayswork.Worker;
using Microsoft.Xna.Framework;
using StardewValley;

namespace Dayswork.Orchestration;

/// <summary>
/// Per-shift passability-grid cache. The first route-cost query against a location does one full
/// <c>isCollidingPosition</c> sweep to build a <see cref="PassabilityGrid"/>; every later query
/// against that location reuses it. This turns the hot per-work-item route selection (which calls
/// <c>ComputeRouteCostsFrom</c> hundreds of times per shift) from hundreds of whole-map collision
/// sweeps into one sweep per location.
///
/// <para>Lives on <see cref="ShiftSession"/>, so constructing a fresh session is the reset. Keyed by
/// <c>NameOrUniqueName</c> (interiors resolve only by <c>NameOrUniqueName</c> — known landmine).</para>
///
/// <para><b>Staleness is tolerated by design.</b> A wrong reachability answer only degrades routing
/// quality — a nav failure defers the work item (retried later; the stuck detector is the terminal
/// backstop) — never item safety, because actual navigation validates against the live probe, not
/// this cache. Invalidation (worker-cleared clumps, SMAPI world-list changes, stuck-recovery
/// rebuild) keeps it fresh enough that the degraded path is rarely hit.</para>
/// </summary>
internal sealed class LocationPassabilityCache
{
    private readonly Dictionary<string, PassabilityGrid> _grids = new(StringComparer.Ordinal);

    public PassabilityGrid GetGrid(GameLocation location)
    {
        var key = location.NameOrUniqueName;
        if (!_grids.TryGetValue(key, out var grid))
        {
            grid = BuildGrid(location);
            _grids[key] = grid;
        }

        return grid;
    }

    /// <summary>Whole-map route-cost flood fill for the given source, over the cached grid.</summary>
    public IReadOnlyDictionary<TileCoord, int> RouteCostsFrom(TileCoord source, GameLocation location) =>
        GridPathfinder.ComputeRouteCosts(GetGrid(location), source);

    /// <summary>Re-probe a single tile after the world changed under it (no-op if not cached).</summary>
    public void InvalidateTile(GameLocation location, int x, int y)
    {
        if (_grids.TryGetValue(location.NameOrUniqueName, out var grid))
            grid.SetPassable(x, y, WorkerMovementDriver.IsTilePassableForWorker(new Point(x, y), location));
    }

    public void InvalidateTile(GameLocation location, TileCoord tile) => InvalidateTile(location, tile.X, tile.Y);

    /// <summary>
    /// Re-probe every tile in an inclusive footprint rectangle — used for multi-tile obstacles
    /// (resource clumps, buildings) whose removal frees several tiles at once.
    /// </summary>
    public void InvalidateArea(GameLocation location, int left, int top, int width, int height)
    {
        if (!_grids.TryGetValue(location.NameOrUniqueName, out var grid))
            return;

        for (var y = top; y < top + height; y++)
            for (var x = left; x < left + width; x++)
                grid.SetPassable(x, y, WorkerMovementDriver.IsTilePassableForWorker(new Point(x, y), location));
    }

    /// <summary>Drop a location's grid entirely so it rebuilds fresh on the next query.</summary>
    public void InvalidateLocation(GameLocation location) => _grids.Remove(location.NameOrUniqueName);

    private static PassabilityGrid BuildGrid(GameLocation location)
    {
        var layer = location.Map.Layers[0];
        return new PassabilityGrid(
            layer.LayerWidth,
            layer.LayerHeight,
            (x, y) => WorkerMovementDriver.IsTilePassableForWorker(new Point(x, y), location));
    }
}
