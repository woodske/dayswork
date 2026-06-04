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

    private bool IsExpansionGreenhouseBatch(WorkBatch batch) =>
        batch.Kind == BatchKind.Greenhouse &&
        ModEntry.ExpansionCompat is { } compat &&
        compat.TryGetExpansionLocationDescriptor(batch.LocationName, out var descriptor) &&
        descriptor.Role == ExpansionLocationRole.GreenhouseWork;

    private bool TryStartExpansionRoute(
        string sourceLocationName,
        string targetLocationName,
        ExpansionRoutePurpose purpose,
        PendingExpansionRouteKind routeKind,
        WorkBatch? batch)
    {
        if (_farmhand is null || ModEntry.ExpansionCompat is not { } compat)
            return false;

        var farm = Game1.getFarm();
        if (!compat.TryValidateRoute(
                farm,
                sourceLocationName,
                targetLocationName,
                purpose,
                out var route,
                out var failure))
        {
            LogExpansionRouteFailure(failure);
            return false;
        }

        _pendingExpansionRouteKind = routeKind;
        _pendingExpansionRouteBatch = batch;
        _toolAnimator.StopSwing();
        EnsureWorkingIntent(new IntentMoveToTile(route.Hops[0].Hop.ApproachTile));
        _expansionRouteNavigator.Start(route, _farmhand);
        _currentLocation = route.Hops[0].Source;
        return true;
    }

    private void HandleExpansionRouteMovement()
    {
        _expansionRouteNavigator.Update();
        if (_farmhand?.currentLocation is { } location)
            _currentLocation = location;

        if (_expansionRouteNavigator.NavigationFailed)
        {
            FailPendingExpansionRoute(_expansionRouteNavigator.Failure);
            return;
        }

        if (!_expansionRouteNavigator.IsComplete)
            return;

        var completedKind = _pendingExpansionRouteKind;
        _pendingExpansionRouteKind = PendingExpansionRouteKind.None;
        _expansionRouteNavigator.Clear();

        switch (completedKind)
        {
            case PendingExpansionRouteKind.WorkEntry:
                CompleteExpansionWorkEntry();
                break;
            case PendingExpansionRouteKind.WorkExit:
                CompleteExpansionWorkExit();
                break;
            case PendingExpansionRouteKind.DepositEntry:
                CompleteExpansionDepositEntry();
                break;
            case PendingExpansionRouteKind.DepositExit:
                FinalizeAndAdvanceTrip(Game1.getFarm());
                break;
        }
    }

    private void CompleteExpansionWorkEntry()
    {
        if (_ctx is null || _farmhand is null)
            return;

        var batch = _pendingExpansionRouteBatch ?? _ctx.Batches[_ctx.CurrentBatchIndex];
        _pendingExpansionRouteBatch = null;
        var location = _farmhand.currentLocation ?? Game1.getLocationFromName(batch.LocationName);
        if (location is null)
        {
            _ctx.CurrentBatchIndex++;
            BeginCurrentBatch();
            return;
        }

        _currentLocation = location;
        var batchTasks = batch.Tasks.ToHashSet();
        var tileWork = _indoorScanner.ScanInterior(
            location,
            batchTasks,
            _ctx.ToolSnapshot,
            OutputScopeProvenance.Greenhouse(batch.LocationName));

        QueueBatchWork(batch with { TileWork = tileWork, AnimalWork = Array.Empty<AnimalWorkItem>() }, location);
        StartNextAnimalOrTileOrAdvance();
    }

    private void CompleteExpansionWorkExit()
    {
        _pendingExpansionRouteBatch = null;
        _currentLocation = Game1.getFarm();
        if (_ctx is null)
            return;

        _ctx.CurrentBatchIndex++;
        BeginCurrentBatch();
    }

    private void CompleteExpansionDepositEntry()
    {
        if (_currentTrip is not { Destination: ChestDestination chestDest })
        {
            FinalizeAndAdvanceTrip(Game1.getFarm());
            return;
        }

        _ctx!.StateMachine.SetIntent(ToDepositIntent(_currentTrip));
        StartChestDepositNavigation(_currentTrip, chestDest, _currentLocation ?? Game1.getFarm());
    }

    private void FailPendingExpansionRoute(ExpansionRouteFailure? failure)
    {
        if (failure is not null)
            LogExpansionRouteFailure(failure);

        var failedKind = _pendingExpansionRouteKind;
        _pendingExpansionRouteKind = PendingExpansionRouteKind.None;
        _pendingExpansionRouteBatch = null;
        _expansionRouteNavigator.Clear();

        switch (failedKind)
        {
            case PendingExpansionRouteKind.WorkEntry:
                if (_ctx is not null)
                {
                    _ctx.CurrentBatchIndex++;
                    BeginCurrentBatch();
                }
                break;
            case PendingExpansionRouteKind.WorkExit:
                WarpExpansionWorkerToFarm();
                CompleteExpansionWorkExit();
                break;
            case PendingExpansionRouteKind.DepositEntry:
                if (_currentTrip is not null)
                    MarkDepositTripUndelivered(_currentTrip);
                FinalizeAndAdvanceTrip(Game1.getFarm());
                break;
            case PendingExpansionRouteKind.DepositExit:
                WarpExpansionWorkerToFarm();
                FinalizeAndAdvanceTrip(Game1.getFarm());
                break;
        }
    }

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

    private void CompleteBuildingEntry()
    {
        if (_ctx is null || _farmhand is null || _pendingBuildingInterior is null)
            return;

        var batch = _ctx.Batches[_ctx.CurrentBatchIndex];
        var interior = _pendingBuildingInterior;
        var entryTile = _buildingNavigator.ResolveInteriorEntryTile(interior);
        _buildingNavigator.Enter(_farmhand, interior, entryTile);
        _currentLocation = interior;
        _pendingBuildingEntry = false;
        _pendingBuildingInterior = null;

        IReadOnlyList<WorkItem> tileWork;
        IReadOnlyList<AnimalWorkItem> animalWork;
        var batchTasks = batch.Tasks.ToHashSet();

        if (batch.Kind == BatchKind.AnimalBuilding)
        {
            tileWork = _indoorScanner.ScanInterior(
                interior,
                batchTasks,
                _ctx.ToolSnapshot,
                OutputScopeProvenance.AnimalBuilding(batch.LocationName));
            if (batch.FeedBuilding)
            {
                _currentFeedPlan = _animalHandler.CreateFeedWork(interior);
                tileWork = _currentFeedPlan.WorkItems.Concat(tileWork).ToList();
                _hayInHand = 0;
            }
            else
            {
                _currentFeedPlan = null;
                _hayInHand = 0;
            }

            var selectedHome = new HashSet<string>(StringComparer.Ordinal) { batch.LocationName };
            animalWork = BuildAnimalWork(interior, selectedHome, batchTasks);
        }
        else
        {
            _currentFeedPlan = null;
            _hayInHand = 0;
            tileWork = _indoorScanner.ScanInterior(
                interior,
                batchTasks,
                _ctx.ToolSnapshot,
                OutputScopeProvenance.Greenhouse(batch.LocationName));
            animalWork = Array.Empty<AnimalWorkItem>();
        }

        QueueBatchWork(batch with { TileWork = tileWork, AnimalWork = animalWork }, interior);
        StartNextAnimalOrTileOrAdvance();
    }
}
