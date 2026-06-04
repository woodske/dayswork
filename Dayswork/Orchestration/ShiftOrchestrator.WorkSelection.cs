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
    private static string NormalizeGreenhouseLocationName(Farm farm, string requestedName)
    {
        if (ModEntry.ExpansionCompat is { } compat &&
            compat.TryGetExpansionLocationDescriptor(requestedName, out _))
            return requestedName;

        return BuildingLocationResolver.NormalizeLocationName(farm, requestedName);
    }

    private bool IsAnimalHouseLocation(string locationName) =>
        _buildingNavigator.TryResolveInterior(locationName, out var interior) && interior is AnimalHouse;

    private void QueueBatchWork(WorkBatch batch, GameLocation location)
    {
        _ctx!.WorkList.Clear();
        foreach (var item in batch.TileWork)
            _ctx.WorkList.Enqueue(item);

        _animalWork.Clear();
        foreach (var item in batch.AnimalWork)
            _animalWork.Enqueue(item);

        _deferredTileWork.Clear();
        _deferredAnimalWork.Clear();
        _currentTileWork = null;
        _currentAnimalWork = null;
        _activeBatchSelectionAttempts = 0;
        _activeBatchMaxSelectionAttempts = Math.Max(4, (batch.TileWork.Count + batch.AnimalWork.Count + 1) * 4);
        _currentLocation = location;
    }

    private void StartNextAnimalOrTileOrAdvance()
    {
        if (_ctx is null || _farmhand is null)
            return;

        _stuck.Reset();
        _actionPending = false;

        if (ShouldWrapUpBeforeNextUnit())
        {
            QueueWrapUpNow(_ctx.PendingStopReason ?? ShiftStopReason.Exhausted);
            return;
        }

        PruneStaleActiveWork(_currentLocation ?? Game1.getFarm());

        if (_activeBatchSelectionAttempts++ > _activeBatchMaxSelectionAttempts)
        {
            ModEntry.ModMonitor.Log(
                $"[Dayswork][routing] active-batch selection guard fired; skipping remaining blocked work. tile={_ctx.WorkList.Count} animal={_animalWork.Count} deferredTile={_deferredTileWork.Count} deferredAnimal={_deferredAnimalWork.Count}.",
                LogLevel.Warn);
            ClearRemainingActiveBatchWork();
            CompleteCurrentBatch();
            return;
        }

        if (TrySelectNextActiveWork(_currentLocation ?? Game1.getFarm(), out var candidate, out var selectedRoute))
        {
            DispatchSelectedActiveWork(candidate, selectedRoute.InteractionTile);
            return;
        }

        if (_ctx.WorkList.Count > 0 ||
            _animalWork.Count > 0 ||
            _deferredTileWork.Count > 0 ||
            _deferredAnimalWork.Count > 0)
        {
            ModEntry.ModMonitor.Log(
                $"[Dayswork][routing] no reachable active-batch work remains; skipping blocked work. tile={_ctx.WorkList.Count} animal={_animalWork.Count} deferredTile={_deferredTileWork.Count} deferredAnimal={_deferredAnimalWork.Count}.",
                LogLevel.Trace);
            ClearRemainingActiveBatchWork();
        }

        // Before we declare the FarmForage batch finished, give the pigs one more chance: re-scan
        // the farm for any truffles (or other animal-product forage) that spawned while the worker
        // was picking up the earlier ones. If new work appears, requeue it and let the next tick
        // route to it instead of completing the batch.
        if (TryRescanFarmForageBeforeBatchComplete())
            return;

        CompleteCurrentBatch();
    }

    private bool TryRescanFarmForageBeforeBatchComplete()
    {
        if (_ctx is null) return false;
        if (_ctx.CurrentBatchIndex >= _ctx.Batches.Count) return false;

        var batch = _ctx.Batches[_ctx.CurrentBatchIndex];
        if (batch.Kind != BatchKind.FarmForage) return false;

        var batchTasks = batch.Tasks.ToHashSet();
        if (!batchTasks.Contains(TaskKind.CollectAnimalProducts)) return false;

        // Reset the per-batch re-enqueue guard whenever we start rescanning a different batch.
        if (_ctx.CurrentBatchIndex != _rescanBatchIndex)
        {
            _rescanBatchIndex = _ctx.CurrentBatchIndex;
            _rescanEnqueuedTiles.Clear();
        }

        var farm = _currentLocation ?? Game1.getFarm();
        var freshTileWork = _workAreaScanner.ScanWholeLocation(
            farm,
            batchTasks,
            _ctx.ToolSnapshot,
            _farmExitTile,
            OutputScopeProvenance.AnimalBuilding(string.Empty));
        if (freshTileWork.Count == 0) return false;

        // Dedupe against anything we're already routing to (active queue, deferred, current).
        // ClearRemainingActiveBatchWork ran above only if there was blocked work; either way,
        // anything we don't already know about is genuinely new.
        var seen = new HashSet<(TaskKind, TileCoord)>();
        foreach (var item in _ctx.WorkList)
            seen.Add((item.Task, item.TaskTile));
        foreach (var item in _deferredTileWork)
            seen.Add((item.Task, item.TaskTile));
        if (_currentTileWork is not null)
            seen.Add((_currentTileWork.Task, _currentTileWork.TaskTile));

        var added = 0;
        foreach (var item in freshTileWork)
        {
            // Skip tiles this batch's rescan already enqueued once — they were either unreachable or
            // not actually removable, so re-adding them would loop forever (Bug: continuous
            // "rescan picked up 1 new tile item"). Genuinely new forage at fresh tiles still flows.
            if (!_rescanEnqueuedTiles.Contains(item.TaskTile) &&
                seen.Add((item.Task, item.TaskTile)))
            {
                _ctx.WorkList.Enqueue(item);
                _rescanEnqueuedTiles.Add(item.TaskTile);
                added++;
            }
        }
        if (added == 0) return false;

        // Reset the routing guard so it doesn't immediately fire on the items we just queued.
        _activeBatchSelectionAttempts = 0;

        DevLog.Log($"[Dayswork][farm-forage] pre-completion rescan picked up {added} new tile item(s); batch continues.");
        return true;
    }

    private bool TrySelectNextActiveWork(
        GameLocation location,
        out ActiveWorkCandidate candidate,
        out WorkerRouteCandidate selectedRoute)
    {
        var candidates = BuildActiveWorkCandidates(location);
        var evaluated = new List<WorkerRouteCandidate>(candidates.Count);
        var source = new TileCoord(_farmhand!.TilePoint.X, _farmhand.TilePoint.Y);
        var routeCosts = WorkerMovementDriver.ComputeRouteCostsFrom(source, location);

        for (var i = 0; i < candidates.Count; i++)
        {
            if (TryEvaluateCandidateRoute(candidates[i], i, routeCosts, out var routeCandidate))
                evaluated.Add(routeCandidate);
        }

        var selected = _routeSelector.Select(evaluated);
        if (selected is null)
        {
            candidate = default!;
            selectedRoute = default!;
            return false;
        }

        candidate = candidates[selected.CandidateId];
        selectedRoute = selected;
        return true;
    }

    private List<ActiveWorkCandidate> BuildActiveWorkCandidates(GameLocation location)
    {
        var candidates = new List<ActiveWorkCandidate>();
        var stableOrder = 0;

        foreach (var item in _ctx!.WorkList)
        {
            if (!IsTileWorkActionable(item, location))
                continue;

            candidates.Add(new ActiveWorkCandidate(
                TileWork: item,
                AnimalWork: null,
                Task: item.Task,
                TaskTile: item.TaskTile,
                NavigationTiles: item.NavigationTiles,
                StableOrder: stableOrder++));
        }

        foreach (var item in _animalWork)
        {
            var animal = _animalHandler.FindLiveAnimal(location, item.Animal);
            if (animal is null || !IsAnimalWorkActionable(item, animal))
                continue;

            candidates.Add(new ActiveWorkCandidate(
                TileWork: null,
                AnimalWork: item,
                Task: item.Task,
                TaskTile: _animalHandler.CurrentTile(animal),
                NavigationTiles: _animalHandler.CurrentNavigationTiles(animal, location),
                StableOrder: stableOrder++));
        }

        return candidates;
    }

    private bool TryEvaluateCandidateRoute(
        ActiveWorkCandidate candidate,
        int candidateId,
        IReadOnlyDictionary<TileCoord, int> routeCosts,
        out WorkerRouteCandidate routeCandidate)
    {
        var bestCost = int.MaxValue;
        TileCoord? bestTile = null;

        foreach (var navTile in candidate.NavigationTiles.Distinct())
        {
            if (!routeCosts.TryGetValue(navTile, out var routeCost))
                continue;

            if (routeCost < bestCost)
            {
                bestCost = routeCost;
                bestTile = navTile;
            }
        }

        if (bestTile is null)
        {
            routeCandidate = default!;
            return false;
        }

        routeCandidate = new WorkerRouteCandidate(
            CandidateId: candidateId,
            Task: candidate.Task,
            PriorityRank: _priorityOrderer.Rank(candidate.Task),
            StableOrder: candidate.StableOrder,
            InteractionTile: bestTile.Value,
            Reachable: true,
            RouteCost: bestCost);
        return true;
    }

    private void DispatchSelectedActiveWork(ActiveWorkCandidate candidate, TileCoord navTile)
    {
        var location = _currentLocation ?? Game1.getFarm();
        if (candidate.TileWork is { } tileWork)
        {
            RemoveFirstQueued(_ctx!.WorkList, tileWork);
            StartNextTileWork(tileWork with { NavTile = navTile });
            return;
        }

        if (candidate.AnimalWork is { } animalWork)
        {
            RemoveFirstQueued(_animalWork, animalWork);
            StartAnimalWork(animalWork, navTile, location);
        }
    }

    private void StartAnimalWork(AnimalWorkItem next, TileCoord navTile, GameLocation location)
    {
        var animal = _animalHandler.FindLiveAnimal(location, next.Animal);
        if (animal is null || !IsAnimalWorkActionable(next, animal))
        {
            StartNextAnimalOrTileOrAdvance();
            return;
        }

        _currentAnimalWork = next;
        _currentTileWork = null;
        _pendingTask = next.Task;
        _pendingNavTile = navTile;
        _pendingTaskTile = _animalHandler.CurrentTile(animal);
        _pendingOutputProvenance = next.Provenance;
        _toolAnimator.StopSwing();
        _toolAnimator.OnTaskChanged(_pendingTask, next.Task);
        EnsureWorkingIntent(new IntentMoveToTile(navTile));
        _nav.StartNavigation(navTile, location, _farmhand!);
    }

    private void PruneStaleActiveWork(GameLocation location)
    {
        RemoveWhere(_ctx!.WorkList, item => IsTileWorkStale(item, location));
        RemoveWhere(_animalWork, item =>
        {
            var animal = _animalHandler.FindLiveAnimal(location, item.Animal);
            return animal is null || !IsAnimalWorkActionable(item, animal);
        });
    }

    private bool IsTileWorkActionable(WorkItem item, GameLocation location)
    {
        if (IsTileWorkStale(item, location))
            return false;

        if (item.Task != TaskKind.FeedAnimals)
            return true;

        if (_currentFeedPlan is null)
            return false;

        if (item.TaskTile == _currentFeedPlan.HopperTile)
            return _hayInHand <= 0;

        return _hayInHand > 0 && IsEmptyTrough(item.TaskTile, location);
    }

    private bool IsTileWorkStale(WorkItem item, GameLocation location)
    {
        if (item.Task != TaskKind.FeedAnimals)
            return IsTaskComplete(item.TaskTile, item.Task, location);

        if (_currentFeedPlan is null)
            return true;

        if (item.TaskTile == _currentFeedPlan.HopperTile)
            return _hayInHand > 0;

        if (_hayInHand <= 0)
            return false;

        return !IsEmptyTrough(item.TaskTile, location);
    }

    private static bool IsEmptyTrough(TileCoord tile, GameLocation location)
    {
        var tileVec = new Vector2(tile.X, tile.Y);
        return !location.objects.ContainsKey(tileVec) &&
               location.doesTileHaveProperty(tile.X, tile.Y, "Trough", "Back", false) is not null;
    }

    private bool IsAnimalWorkActionable(AnimalWorkItem item, FarmAnimal animal) =>
        item.Task switch
        {
            TaskKind.PetAnimals => _animalHandler.ShouldPet(animal),
            TaskKind.CollectAnimalProducts => _animalHandler.HasToolHarvestReady(animal),
            _ => false,
        };

    private void RecordActiveBatchProgress()
    {
        foreach (var item in _deferredTileWork)
            _ctx!.WorkList.Enqueue(item);
        foreach (var item in _deferredAnimalWork)
            _animalWork.Enqueue(item);

        _deferredTileWork.Clear();
        _deferredAnimalWork.Clear();
        _activeBatchSelectionAttempts = 0;
    }

    private void ClearRemainingActiveBatchWork()
    {
        _ctx!.WorkList.Clear();
        _animalWork.Clear();
        _deferredTileWork.Clear();
        _deferredAnimalWork.Clear();
        _currentTileWork = null;
        _currentAnimalWork = null;
    }

    private static bool RemoveFirstQueued<T>(Queue<T> queue, T value)
    {
        var removed = false;
        var count = queue.Count;
        for (var i = 0; i < count; i++)
        {
            var current = queue.Dequeue();
            if (!removed && EqualityComparer<T>.Default.Equals(current, value))
            {
                removed = true;
                continue;
            }

            queue.Enqueue(current);
        }

        return removed;
    }

    private static void RemoveWhere<T>(Queue<T> queue, Func<T, bool> predicate)
    {
        var count = queue.Count;
        for (var i = 0; i < count; i++)
        {
            var current = queue.Dequeue();
            if (!predicate(current))
                queue.Enqueue(current);
        }
    }

    private void StartNextAnimalWork()
    {
        while (_animalWork.Count > 0)
        {
            var next = _animalWork.Dequeue();
            var location = _currentLocation ?? Game1.getFarm();
            var animal = _animalHandler.FindLiveAnimal(location, next.Animal);
            if (animal is null)
                continue;

            if (next.Task == TaskKind.PetAnimals && !_animalHandler.ShouldPet(animal))
                continue;

            if (next.Task == TaskKind.CollectAnimalProducts && !_animalHandler.HasToolHarvestReady(animal))
                continue;

            var navTile = _animalHandler.CurrentNavigationTile(animal, location);
            if (navTile is null)
                continue;

            _currentAnimalWork = next;
            _pendingTask = next.Task;
            _pendingNavTile = navTile.Value;
            _pendingTaskTile = _animalHandler.CurrentTile(animal);
            _pendingOutputProvenance = next.Provenance;
            _toolAnimator.StopSwing();
            _toolAnimator.OnTaskChanged(_pendingTask, next.Task);
            EnsureWorkingIntent(new IntentMoveToTile(navTile.Value));
            _nav.StartNavigation(navTile.Value, location, _farmhand!);
            return;
        }

        StartNextAnimalOrTileOrAdvance();
    }

    private void StartNextTileWork(WorkItem next)
    {
        var previousTask = _pendingTask;
        _pendingTask = next.Task;
        _pendingNavTile = next.NavTile;
        _pendingTaskTile = next.TaskTile;
        _pendingOutputProvenance = next.Provenance ?? OutputScopeProvenance.Unknown;
        _currentTileWork = next;
        _currentAnimalWork = null;
        _toolAnimator.StopSwing();
        _toolAnimator.OnTaskChanged(previousTask, next.Task);
        EnsureWorkingIntent(new IntentMoveToTile(next.NavTile));
        _nav.StartNavigation(next.NavTile, _currentLocation ?? Game1.getFarm(), _farmhand!);
    }
}
