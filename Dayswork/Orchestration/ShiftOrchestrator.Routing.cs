using Dayswork.Core.Config;
using Dayswork.Compat;
using Dayswork.Core.Compat;
using Dayswork.Core.Domain;
using Dayswork.Core.Energy;
using Dayswork.Core.Inventory;
using Dayswork.Core.Pricing;
using Dayswork.Core.Shifts;
using Dayswork.Integration;
using Dayswork.Worker;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;

namespace Dayswork.Orchestration;

internal sealed partial class ShiftOrchestrator
{
    private static TileCoord ResolveSpawnExitTile(Farm farm)
    {
        var building = HiringBuildingInteraction.FindHiringBuilding(farm);
        if (building is not null)
        {
            var door = building.getPointForHumanDoor();
            return ResolvePassableNearby(new TileCoord(door.X, door.Y + 1), farm);
        }

        return FindFarmExitTile(farm);
    }

    private static TileCoord FindFarmExitTile(Farm farm)
    {
        // SVE/expansion entrance override (U-SVE-02): consult the compat seam first. If it supplies
        // a verified per-map entrance for this farm's signature, use it; otherwise fall through to
        // the existing warp heuristic (FR-SVE-06). The override is best-effort and never throws.
        if (ModEntry.ExpansionCompat is { } compat &&
            compat.TryGetFarmEntranceOverride(farm, out var overrideTile))
            return ResolvePassableNearby(new TileCoord(overrideTile.X, overrideTile.Y), farm);

        // Build the set of interior location names so we can skip building-entry warps.
        var interiorNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var building in farm.buildings)
        {
            var interior = building.indoors.Value;
            if (interior is not null)
                interiorNames.Add(interior.NameOrUniqueName);
        }

        var mapLayer = farm.Map.Layers[0];

        foreach (var warp in farm.warps)
        {
            if (interiorNames.Contains(warp.TargetName))
                continue;

            // Secondary guard: the farmhouse/cellar may not appear in farm.buildings in
            // some SDV versions.  Skip any warp whose resolved target is an indoor location.
            var targetLocation = Game1.getLocationFromName(warp.TargetName);
            if (targetLocation is not null && !targetLocation.IsOutdoors)
                continue;

            // Found an outdoor exit warp.
            //
            // Clamp to the map boundary and compute the inward approach direction.
            // A warp at X=80 on an 80-wide map is outside the map; clamped to X=79 with
            // approach direction dx=-1 (search leftward into the exit corridor).
            var cx = Math.Clamp(warp.X, 0, mapLayer.LayerWidth  - 1);
            var cy = Math.Clamp(warp.Y, 0, mapLayer.LayerHeight - 1);

            int dx = 0, dy = 0;
            if      (warp.X >= mapLayer.LayerWidth)  dx = -1;  // right edge  → search left
            else if (warp.X < 0)                     dx =  1;  // left edge   → search right
            else if (warp.Y >= mapLayer.LayerHeight) dy = -1;  // bottom edge → search up
            else if (warp.Y < 0)                     dy =  1;  // top edge    → search down
            // dx=dy=0: warp is within bounds; the loop checks step 0 only (the tile itself)
            // then falls through to the adjacent-tile search if needed.

            int steps = (dx != 0 || dy != 0) ? 10 : 1;
            for (int step = 0; step < steps; step++)
            {
                var tx = cx + dx * step;
                var ty = cy + dy * step;
                if (tx < 0 || ty < 0 || tx >= mapLayer.LayerWidth || ty >= mapLayer.LayerHeight)
                    break;

                if (WorkerMovementDriver.IsTilePassableForWorker(new Point(tx, ty), farm))
                {
//                     ModEntry.ModMonitor.Log(
//                         $"[Dayswork] Farm exit: tile ({tx},{ty}) near warp ({warp.X},{warp.Y}) → {warp.TargetName} (step={step}).",
//                         LogLevel.Trace);
                    return new TileCoord(tx, ty);
                }
            }

            // For in-bounds warps where the warp tile itself is impassable, also try
            // the four cardinal neighbours (handles warps set in the middle of the farm).
            if (dx == 0 && dy == 0)
            {
                foreach (var n in new Point[] { new(cx,cy-1), new(cx+1,cy), new(cx,cy+1), new(cx-1,cy) })
                {
                    if (n.X < 0 || n.Y < 0 || n.X >= mapLayer.LayerWidth || n.Y >= mapLayer.LayerHeight)
                        continue;
                    if (!WorkerMovementDriver.IsTilePassableForWorker(n, farm))
                        continue;

//                     ModEntry.ModMonitor.Log(
//                         $"[Dayswork] Farm exit: adjacent tile ({n.X},{n.Y}) near warp ({warp.X},{warp.Y}) → {warp.TargetName}.",
//                         LogLevel.Trace);
                    return new TileCoord(n.X, n.Y);
                }
            }

            // No passable tile found — return the clamped boundary tile; HandleExit will
            // log a warning if navigation ultimately fails.
            ModEntry.ModMonitor.Log(
                $"[Dayswork] Farm exit: no passable approach tile found near warp ({warp.X},{warp.Y}) → {warp.TargetName}.",
                LogLevel.Warn);
            return new TileCoord(cx, cy);
        }

        ModEntry.ModMonitor.Log(
            "[Dayswork] No external farm exit warp found — using fallback tile (77, 15).",
            LogLevel.Warn);
        return new TileCoord(77, 15);
    }

    private static TileCoord ResolvePassableNearby(TileCoord preferred, Farm farm)
    {
        var mapLayer = farm.Map.Layers[0];
        int w = mapLayer.LayerWidth, h = mapLayer.LayerHeight;

        bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < w && y < h;

        if (InBounds(preferred.X, preferred.Y) &&
            WorkerMovementDriver.IsTilePassableForWorker(new Point(preferred.X, preferred.Y), farm))
            return preferred;

        const int maxRadius = 12;
        for (int r = 1; r <= maxRadius; r++)
        {
            for (int dx = -r; dx <= r; dx++)
            for (int dy = -r; dy <= r; dy++)
            {
                // Only the tiles on the ring at Chebyshev distance r (inner rings already searched).
                if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r)
                    continue;

                int x = preferred.X + dx, y = preferred.Y + dy;
                if (!InBounds(x, y))
                    continue;
                if (!WorkerMovementDriver.IsTilePassableForWorker(new Point(x, y), farm))
                    continue;

                ModEntry.ModMonitor.Log(
                    $"[Dayswork] Entrance override ({preferred.X},{preferred.Y}) blocked — using nearby passable tile ({x},{y}).",
                    LogLevel.Trace);
                return new TileCoord(x, y);
            }
        }

        ModEntry.ModMonitor.Log(
            $"[Dayswork] Entrance override ({preferred.X},{preferred.Y}) blocked and no passable tile found within {maxRadius} tiles — using it anyway.",
            LogLevel.Warn);
        return preferred;
    }

    private TileCoord ResolveReachableShiftExitTile(Farm farm)
    {
        if (_farmhand is null)
            return _farmExitTile;

        var source = new TileCoord(_farmhand.TilePoint.X, _farmhand.TilePoint.Y);
        var routeCosts = WorkerMovementDriver.ComputeRouteCostsFrom(source, farm);
        if (routeCosts.ContainsKey(_farmExitTile))
            return _farmExitTile;

        if (WorkerRouteSelector.TrySelectNearestReachableTile(
                EnumerateNearbyPassableTiles(_farmExitTile, farm, maxRadius: 16),
                routeCosts,
                out var fallback))
        {
            DevLog.Log(
                $"[Dayswork][exit] configured exit tile ({_farmExitTile.X},{_farmExitTile.Y}) unreachable from " +
                $"({source.X},{source.Y}); using reachable nearby tile ({fallback.X},{fallback.Y}).",
                LogLevel.Info);
            return fallback;
        }

        ModEntry.ModMonitor.Log(
            $"[Dayswork][exit] configured exit tile ({_farmExitTile.X},{_farmExitTile.Y}) unreachable from " +
            $"({source.X},{source.Y}) and no reachable nearby tile found; using configured tile.",
            LogLevel.Warn);
        return _farmExitTile;
    }

    private static IEnumerable<TileCoord> EnumerateNearbyPassableTiles(TileCoord preferred, Farm farm, int maxRadius)
    {
        var mapLayer = farm.Map.Layers[0];
        var width = mapLayer.LayerWidth;
        var height = mapLayer.LayerHeight;

        bool IsPassable(int x, int y) =>
            x >= 0 &&
            y >= 0 &&
            x < width &&
            y < height &&
            WorkerMovementDriver.IsTilePassableForWorker(new Point(x, y), farm);

        if (IsPassable(preferred.X, preferred.Y))
            yield return preferred;

        for (var radius = 1; radius <= maxRadius; radius++)
        {
            for (var dx = -radius; dx <= radius; dx++)
            {
                for (var dy = -radius; dy <= radius; dy++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != radius)
                        continue;

                    var x = preferred.X + dx;
                    var y = preferred.Y + dy;
                    if (IsPassable(x, y))
                        yield return new TileCoord(x, y);
                }
            }
        }
    }

    private bool IsExpansionGreenhouseBatch(WorkBatch batch) =>
        batch.Kind == BatchKind.Greenhouse &&
        ModEntry.ExpansionCompat is { } compat &&
        compat.TryGetExpansionLocationDescriptor(batch.LocationName, out var descriptor) &&
        descriptor.Role == ExpansionLocationRole.GreenhouseWork;

    private void LogExpansionRouteFailure(ExpansionRouteFailure failure) =>
        ModEntry.ModMonitor.Log(ExpansionCompatService.FormatRouteFailure(failure), LogLevel.Warn);

    private void WarpExpansionWorkerToFarm()
    {
        if (_farmhand is null)
            return;

        var farm = Game1.getFarm();
        var from = _farmhand.currentLocation ?? _currentLocation ?? farm;
        if (from == farm)
        {
            _currentLocation = farm;
            _nav.Clear();
            return;
        }

        _nav.WarpWorker(_farmhand, from, farm, _farmExitTile);
        _currentLocation = farm;
    }

}
