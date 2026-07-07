using Dayswork.Core.Config;
using Dayswork.Compat;
using Dayswork.Core.Compat;
using Dayswork.Core.Domain;
using Dayswork.Core.Energy;
using Dayswork.Core.Geometry;
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
        Session.Ctx.WorkList.Clear();
        foreach (var item in OrderTileWorkForSweep(batch.TileWork, location))
            Session.Ctx.WorkList.Enqueue(item);

        Session.AnimalWork.Clear();
        foreach (var item in batch.AnimalWork)
            Session.AnimalWork.Enqueue(item);

        Session.DeferredTileWork.Clear();
        Session.DeferredAnimalWork.Clear();
        Session.CurrentTileWork = null;
        Session.CurrentAnimalWork = null;
        Session.BatchSelectionAttempts = 0;
        Session.MaxBatchSelectionAttempts = Math.Max(4, (batch.TileWork.Count + batch.AnimalWork.Count + 1) * 4);
        Session.CurrentLocation = location;
    }

    // Tile work executes as a serpentine field sweep: the queue is sorted once per batch, and
    // WorkerRouteCandidate.SelectionKey (= StableOrder = queue position) makes the selector honor
    // that order for non-AnimalCare categories. Same-tile ties put harvest beats before watering
    // so a non-regrow crop isn't watered right before it's picked (wasted beat + energy).
    private IReadOnlyList<WorkItem> OrderTileWorkForSweep(IReadOnlyList<WorkItem> tileWork, GameLocation location)
    {
        if (tileWork.Count <= 1)
            return tileWork;

        var start = Session.Worker is { } worker &&
                    worker.currentLocation is { } current &&
                    SameLocation(current, location)
            ? new TileCoord(worker.TilePoint.X, worker.TilePoint.Y)
            : (TileCoord?)null;

        // Split rows around obstacles (pond/fence/building) so a bisected row is swept one side then
        // the other, instead of detouring across the gap each row. Grid is per-shift cached.
        var grid = Session.Passability.GetGrid(location);
        var sweep = SerpentineSweep.Rank(tileWork.Select(item => item.TaskTile), start, tile => grid.IsPassable(tile));
        return tileWork
            .OrderBy(item => sweep[item.TaskTile])
            .ThenBy(item => item.Task == TaskKind.WaterCrops ? 1 : 0)
            .ToList();
    }

    private void StartNextAnimalOrTileOrAdvance()
    {
        if (_session is null || Session.Worker is null)
            return;

        Session.Stuck.Reset();
        Session.ActionPending = false;

        if (ShouldWrapUpBeforeNextUnit())
        {
            QueueWrapUpNow(Session.Ctx.PendingStopReason ?? ShiftStopReason.Exhausted);
            return;
        }

        PruneStaleActiveWork(Session.CurrentLocation ?? Game1.getFarm());

        if (Session.BatchSelectionAttempts++ > Session.MaxBatchSelectionAttempts)
        {
            ModEntry.ModMonitor.Log(
                $"[Dayswork][routing] active-batch selection guard fired; skipping remaining blocked work. tile={Session.Ctx.WorkList.Count} animal={Session.AnimalWork.Count} deferredTile={Session.DeferredTileWork.Count} deferredAnimal={Session.DeferredAnimalWork.Count}.",
                DevLog.WarnLevel);
            ClearRemainingActiveBatchWork();
            CompleteCurrentBatch();
            return;
        }

        if (TrySelectNextActiveWork(Session.CurrentLocation ?? Game1.getFarm(), out var candidate, out var selectedRoute))
        {
            DispatchSelectedActiveWork(candidate, selectedRoute.InteractionTile);
            return;
        }

        if (Session.Ctx.WorkList.Count > 0 ||
            Session.AnimalWork.Count > 0 ||
            Session.DeferredTileWork.Count > 0 ||
            Session.DeferredAnimalWork.Count > 0)
        {
            ModEntry.ModMonitor.Log(
                $"[Dayswork][routing] no reachable active-batch work remains; skipping blocked work. tile={Session.Ctx.WorkList.Count} animal={Session.AnimalWork.Count} deferredTile={Session.DeferredTileWork.Count} deferredAnimal={Session.DeferredAnimalWork.Count}.",
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
        if (_session is null) return false;
        if (Session.Ctx.CurrentBatchIndex >= Session.Ctx.Batches.Count) return false;

        var batch = Session.Ctx.Batches[Session.Ctx.CurrentBatchIndex];
        if (batch.Kind != BatchKind.FarmForage) return false;

        var batchTasks = batch.Tasks.ToHashSet();
        if (!batchTasks.Contains(TaskKind.CollectAnimalProducts)) return false;

        // Reset the per-batch re-enqueue guard whenever we start rescanning a different batch.
        if (Session.Ctx.CurrentBatchIndex != Session.RescanBatchIndex)
        {
            Session.RescanBatchIndex = Session.Ctx.CurrentBatchIndex;
            Session.RescanEnqueuedTiles.Clear();
        }

        var farm = Session.CurrentLocation ?? Game1.getFarm();
        var freshTileWork = _workAreaScanner.ScanWholeLocation(
            farm,
            batchTasks,
            Session.Ctx.ToolSnapshot,
            Session.FarmExitTile,
            Session.Ctx.Preferences,
            OutputScopeProvenance.AnimalBuilding(string.Empty));
        if (freshTileWork.Count == 0) return false;

        // Dedupe against anything we're already routing to (active queue, deferred, current).
        // ClearRemainingActiveBatchWork ran above only if there was blocked work; either way,
        // anything we don't already know about is genuinely new.
        var seen = new HashSet<(TaskKind, TileCoord)>();
        foreach (var item in Session.Ctx.WorkList)
            seen.Add((item.Task, item.TaskTile));
        foreach (var item in Session.DeferredTileWork)
            seen.Add((item.Task, item.TaskTile));
        if (Session.CurrentTileWork is not null)
            seen.Add((Session.CurrentTileWork.Task, Session.CurrentTileWork.TaskTile));

        var added = 0;
        foreach (var item in freshTileWork)
        {
            // Skip tiles this batch's rescan already enqueued once — they were either unreachable or
            // not actually removable, so re-adding them would loop forever (Bug: continuous
            // "rescan picked up 1 new tile item"). Genuinely new forage at fresh tiles still flows.
            if (!Session.RescanEnqueuedTiles.Contains(item.TaskTile) &&
                seen.Add((item.Task, item.TaskTile)))
            {
                Session.Ctx.WorkList.Enqueue(item);
                Session.RescanEnqueuedTiles.Add(item.TaskTile);
                added++;
            }
        }
        if (added == 0) return false;

        // Reset the routing guard so it doesn't immediately fire on the items we just queued.
        Session.BatchSelectionAttempts = 0;

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
        var source = new TileCoord(Session.Worker!.TilePoint.X, Session.Worker.TilePoint.Y);
        var routeCosts = Session.Passability.RouteCostsFrom(source, location);

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

        foreach (var item in Session.Ctx.WorkList)
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

        foreach (var item in Session.AnimalWork)
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
        var bestRouteCost = int.MaxValue;
        TileCoord? bestTile = null;

        foreach (var navTile in candidate.NavigationTiles.Distinct())
        {
            if (!routeCosts.TryGetValue(navTile.Tile, out var routeCost))
                continue;

            // Diagonal stands carry a +1 travel penalty so an orthogonal stand wins unless the
            // diagonal is 2+ tiles closer (cardinals are listed first → orthogonal wins ties).
            var effectiveCost = routeCost + (navTile.Diagonal ? 1 : 0);
            if (effectiveCost < bestCost)
            {
                bestCost = effectiveCost;
                bestRouteCost = routeCost;
                bestTile = navTile.Tile;
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
            PriorityRank: Session.PriorityOrderer.Rank(candidate.Task),
            StableOrder: candidate.StableOrder,
            // AnimalCare work stays nearest-first (animals move; troughs/floor forage interleave
            // with them at the same rank); every other category follows the precomputed serpentine
            // sweep, whose position is the queue enumeration order (StableOrder).
            SelectionKey: TaskKindSets.CategoryOf(candidate.Task) == TaskCategory.AnimalCare
                ? bestRouteCost
                : candidate.StableOrder,
            InteractionTile: bestTile.Value,
            Reachable: true,
            // Report the chosen tile's real travel cost (not the diagonal-biased value) so candidate
            // ordering keeps reflecting genuine proximity — the bias only steers tile choice.
            RouteCost: bestRouteCost);
        return true;
    }

    private void DispatchSelectedActiveWork(ActiveWorkCandidate candidate, TileCoord navTile)
    {
        var location = Session.CurrentLocation ?? Game1.getFarm();
        if (candidate.TileWork is { } tileWork)
        {
            RemoveFirstQueued(Session.Ctx.WorkList, tileWork);
            StartNextTileWork(tileWork with { NavTile = navTile });
            return;
        }

        if (candidate.AnimalWork is { } animalWork)
        {
            RemoveFirstQueued(Session.AnimalWork, animalWork);
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

        Session.CurrentAnimalWork = next;
        Session.CurrentTileWork = null;
        Session.PendingTask = next.Task;
        Session.PendingNavTile = navTile;
        Session.PendingTaskTile = _animalHandler.CurrentTile(animal);
        Session.PendingOutputProvenance = next.Provenance;
        _toolAnimator.StopSwing();
        _toolAnimator.OnTaskChanged(Session.PendingTask, next.Task);
        EnsureWorkingIntent(new IntentMoveToTile(navTile));
        _nav.StartNavigation(navTile, location, Session.Worker!);
    }

    private void PruneStaleActiveWork(GameLocation location)
    {
        RemoveWhere(Session.Ctx.WorkList, item => IsTileWorkStale(item, location));
        RemoveWhere(Session.AnimalWork, item =>
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

        if (Session.CurrentFeedPlan is null)
            return false;

        if (item.TaskTile == Session.CurrentFeedPlan.HopperTile)
            return Session.HayInHand <= 0;

        return Session.HayInHand > 0 && IsEmptyTrough(item.TaskTile, location);
    }

    private bool IsTileWorkStale(WorkItem item, GameLocation location)
    {
        if (item.Task != TaskKind.FeedAnimals)
            return IsTaskComplete(item.TaskTile, item.Task, location);

        if (Session.CurrentFeedPlan is null)
            return true;

        if (item.TaskTile == Session.CurrentFeedPlan.HopperTile)
            return Session.HayInHand > 0;

        if (Session.HayInHand <= 0)
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
        foreach (var item in Session.DeferredTileWork)
            Session.Ctx.WorkList.Enqueue(item);
        foreach (var item in Session.DeferredAnimalWork)
            Session.AnimalWork.Enqueue(item);

        Session.DeferredTileWork.Clear();
        Session.DeferredAnimalWork.Clear();
        Session.BatchSelectionAttempts = 0;
    }

    private void ClearRemainingActiveBatchWork()
    {
        Session.Ctx.WorkList.Clear();
        Session.AnimalWork.Clear();
        Session.DeferredTileWork.Clear();
        Session.DeferredAnimalWork.Clear();
        Session.CurrentTileWork = null;
        Session.CurrentAnimalWork = null;
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
        while (Session.AnimalWork.Count > 0)
        {
            var next = Session.AnimalWork.Dequeue();
            var location = Session.CurrentLocation ?? Game1.getFarm();
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

            Session.CurrentAnimalWork = next;
            Session.PendingTask = next.Task;
            Session.PendingNavTile = navTile.Value;
            Session.PendingTaskTile = _animalHandler.CurrentTile(animal);
            Session.PendingOutputProvenance = next.Provenance;
            _toolAnimator.StopSwing();
            _toolAnimator.OnTaskChanged(Session.PendingTask, next.Task);
            EnsureWorkingIntent(new IntentMoveToTile(navTile.Value));
            _nav.StartNavigation(navTile.Value, location, Session.Worker!);
            return;
        }

        StartNextAnimalOrTileOrAdvance();
    }

    private void StartNextTileWork(WorkItem next)
    {
        var previousTask = Session.PendingTask;
        Session.PendingTask = next.Task;
        Session.PendingNavTile = next.NavTile;
        Session.PendingTaskTile = next.TaskTile;
        Session.PendingOutputProvenance = next.Provenance ?? OutputScopeProvenance.Unknown;
        Session.CurrentTileWork = next;
        Session.CurrentAnimalWork = null;
        _toolAnimator.StopSwing();
        _toolAnimator.OnTaskChanged(previousTask, next.Task);
        EnsureWorkingIntent(new IntentMoveToTile(next.NavTile));
        _nav.StartNavigation(next.NavTile, Session.CurrentLocation ?? Game1.getFarm(), Session.Worker!);
    }
}
