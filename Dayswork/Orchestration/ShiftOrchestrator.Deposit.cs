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
    private void HandleDeposit(Farm farm)
    {
        if (!_nav.NavigationFailed && !_nav.HasArrived)
            return;

        if (_currentTrip is null)
        {
            // Nothing in flight — advance to the next trip or exit.
            FinalizeAndAdvanceTrip(farm);
            return;
        }

        // Two-phase interior-chest entry: the worker has finished walking to the building's
        // outdoor door (or failed to reach it). Cross the door now and continue to the chest.
        if (_pendingDepositInterior is { } depositInterior)
        {
            if (_nav.NavigationFailed)
                ModEntry.ModMonitor.Log(
                    $"[Dayswork][deposit] could not walk to {depositInterior.Name} door; warping in.",
                    LogLevel.Warn);
            EnterDepositInterior(depositInterior);
            return;
        }

        // Two-phase interior-chest exit: the worker has finished walking to the interior exit
        // door after depositing. Advance the trip, which warps back out to the farm.
        if (_pendingDepositExit)
        {
            _pendingDepositExit = false;
            FinalizeAndAdvanceTrip(farm);
            return;
        }

        if (_nav.NavigationFailed)
        {
            MarkDepositTripUndelivered(_currentTrip);
            FinalizeAndAdvanceTrip(farm);
            return;
        }

        // First arrival at the trip's stand tile: open the chest visually (if applicable),
        // resolve mutex/liveness, and start the first beat. The mutex/missing-chest paths
        // already route the items to overflow inside BeginTripExecution and return false so we skip the loop.
        if (!_currentTripExecutionStarted)
        {
            if (!BeginTripExecution(_currentTrip, _currentLocation ?? farm))
            {
                FinalizeAndAdvanceTrip(farm);
            }
            return;
        }

        // Pacing gate: wait for the per-stack swing beat to finish before depositing the next stack.
        if (_toolAnimator.IsSwinging)
            return;

        if (_currentTripStackIndex < _currentTrip.Items.Count)
        {
            DepositCurrentTripStack();
            _currentTripStackIndex++;
            if (_currentTripStackIndex < _currentTrip.Items.Count)
            {
                // Kick off the next beat by replaying the no-tool reach animation.
                _toolAnimator.PlaySwing(WorkerTool.None, FacingTowardDestination());
            }
            return;
        }

        EndTripExecution();

        // If we deposited inside a building, walk to the interior door before leaving;
        // the warp back to the farm happens once the worker reaches the door.
        if (BeginDepositInteriorExitWalk())
            return;

        FinalizeAndAdvanceTrip(farm);
    }

    private bool BeginDepositInteriorExitWalk()
    {
        if (_farmhand is null ||
            _currentTrip is not { Destination: ChestDestination { Ref.LocationName: not "Farm" } })
            return false;

        if (_currentTrip.Destination is ChestDestination expansionChest &&
            ModEntry.ExpansionCompat is { } compat &&
            compat.IsExpansionDepositLocation(expansionChest.Ref.LocationName))
        {
            var routeSource = (_currentLocation ?? _farmhand.currentLocation)?.NameOrUniqueName
                              ?? expansionChest.Ref.LocationName;
            if (TryStartExpansionRoute(
                    routeSource,
                    "Farm",
                    ExpansionRoutePurpose.ReturnToFarm,
                    PendingExpansionRouteKind.DepositExit,
                    batch: null))
                return true;

            WarpExpansionWorkerToFarm();
            return false;
        }

        var interior = _currentLocation;
        if (interior is null || interior == Game1.getFarm())
            return false;

        var exitCandidates = _buildingNavigator.ResolveInteriorExitApproachTiles(interior);
        var source = new TileCoord(_farmhand.TilePoint.X, _farmhand.TilePoint.Y);
        var routeCosts = WorkerMovementDriver.ComputeRouteCostsFrom(source, interior);
        var exitTile = BuildingWorkNavigator.SelectNearestReachableExitApproachTile(
            exitCandidates,
            routeCosts,
            exitCandidates[0]);

        _pendingDepositExit = true;
        _nav.StartNavigation(exitTile, interior, _farmhand);
        return true;
    }

    private void FinalizeAndAdvanceTrip(Farm farm)
    {
        if (_currentTrip is not null)
            CompleteDepositTripLocation(_currentTrip);

        _currentTrip = null;
        _currentTripExecutionStarted = false;
        _currentTripStackIndex = 0;
        _currentTripChest = null;
        _currentTripLocation = null;
        _currentTripChestAnimated = false;

        if (_depositTrips.Count > 0)
        {
            var next = _depositTrips.Dequeue();
            _currentTrip = next;
            _ctx!.StateMachine.SetIntent(ToDepositIntent(next));
            StartDepositTrip(next);
            return;
        }

        BeginExit(farm);
    }

    private bool BeginTripExecution(DepositTrip trip, GameLocation location)
    {
        _currentTripExecutionStarted = true;
        _currentTripStackIndex = 0;
        _currentTripChest = null;
        _currentTripLocation = location;
        _currentTripChestAnimated = false;

        if (trip.Destination is ChestDestination chestDest)
        {
            var chest = _chestResolver.ResolveChest(chestDest.Ref);
            if (chest is null)
            {
                // Chest moved/destroyed (FR-OUT-03): everything for it goes to automatic overflow.
                foreach (var stack in trip.Items)
                    _ctx!.Overflow.Add(new OverflowItem(stack, OverflowReason.ChestMissing));
                ModEntry.ModMonitor.Log(
                    $"[Dayswork][deposit] chest missing at ({chestDest.Ref.Tile.X},{chestDest.Ref.Tile.Y}); {trip.Items.Count} stack(s) routed to automatic overflow.",
                    LogLevel.Trace);
                return false;
            }

            if (chest.GetMutex().IsLocked())
            {
                // A farmer (player) has the chest UI open. Defer the whole trip to overflow
                // rather than mutating items behind the player's back.
                foreach (var stack in trip.Items)
                    _ctx!.Overflow.Add(new OverflowItem(stack, OverflowReason.ChestBusy));
                ModEntry.ModMonitor.Log(
                    $"[Dayswork][deposit] chest busy at ({chestDest.Ref.Tile.X},{chestDest.Ref.Tile.Y}); {trip.Items.Count} stack(s) routed to automatic overflow.",
                    LogLevel.Trace);
                return false;
            }

            _currentTripChest = chest;

            // Only animate / play sound if the player is in the chest's location.
            // SDV's location.playSound is already location-scoped; the playerHere guard also
            // skips the lid frame mutation when nobody is around to see it.
            if (chest.Location is { } chestLoc && Game1.player.currentLocation == chestLoc)
            {
                chest.frameCounter.Value = 5;  // vanilla open trigger
                chestLoc.playSound("openChest", new Vector2(chest.TileLocation.X, chest.TileLocation.Y));
                _currentTripChestAnimated = true;
            }
        }
        // (Shipping bin: per-stack Farm.shipItem handles the bin lid + sound; no trip-level open.)

        // Start the first beat: a no-tool reach animation facing the destination tile.
        _toolAnimator.PlaySwing(WorkerTool.None, FacingTowardDestination());
        return true;
    }

    private void DepositCurrentTripStack()
    {
        if (_currentTrip is null || _ctx is null)
            return;

        var stack = _currentTrip.Items[_currentTripStackIndex];
        var loc = _currentTripLocation;
        var playerHere = loc is not null && Game1.player.currentLocation == loc;

        if (_currentTripChest is { } chest)
        {
            // Re-check the mutex per stack: if the player just opened the chest UI in the
            // middle of our deposit, abort the rest of the trip and route what remains to overflow.
            if (chest.GetMutex().IsLocked())
            {
                for (var i = _currentTripStackIndex; i < _currentTrip.Items.Count; i++)
                    _ctx.Overflow.Add(new OverflowItem(_currentTrip.Items[i], OverflowReason.ChestBusy));
                _currentTripStackIndex = _currentTrip.Items.Count;  // skip ahead to "trip complete"
                ModEntry.ModMonitor.Log(
                    $"[Dayswork][deposit] chest became busy mid-trip; remaining stacks routed to automatic overflow.",
                    LogLevel.Trace);
                return;
            }

            DepositIntoChest(chest, stack);
            if (playerHere && chest.Location is { } chestLoc)
                chestLoc.playSound("Ship", new Vector2(chest.TileLocation.X, chest.TileLocation.Y));
            return;
        }

        DepositIntoShippingBin(stack, animateWhenPlayerHere: playerHere);
    }

    private static bool IsDepositErrorItem(Item item) =>
        item is null
        || string.IsNullOrEmpty(item.ItemId)
        || item.QualifiedItemId == "(O)"
        || string.Equals(item.Name, "Error Item", StringComparison.Ordinal);

    private static void DepositIntoShippingBin(RoutedItemStack stack, bool animateWhenPlayerHere)
    {
        var farm = Game1.getFarm();
        if (string.IsNullOrWhiteSpace(stack.QualifiedItemId))
        {
            ModEntry.ModMonitor.Log(
                $"[Dayswork][deposit] Error Item suppressed at shipping bin: empty QualifiedItemId stack=x{stack.Quantity} task={stack.SourceTask}; not shipped.",
                LogLevel.Error);
            return;
        }
        var item = ItemRegistry.Create(stack.QualifiedItemId, stack.Quantity);
        if (item is null)
            return;
        if (IsDepositErrorItem(item))
        {
            ModEntry.ModMonitor.Log(
                $"[Dayswork][deposit] Error Item suppressed at shipping bin: rawId='{stack.QualifiedItemId}' resolvedQualifiedId='{item.QualifiedItemId}' name='{item.Name}' stack=x{stack.Quantity} task={stack.SourceTask}; not shipped.",
                LogLevel.Error);
            return;
        }

        if (animateWhenPlayerHere && Game1.player.currentLocation == farm)
            farm.shipItem(item, Game1.player);           // vanilla lid animation + backpackIN + delayed "Ship"
        else
            farm.getShippingBin(Game1.player).Add(item); // silent fallback
    }

    private void EndTripExecution()
    {
        if (_currentTripChest is { } chest && _currentTripChestAnimated)
        {
            // Vanilla close trigger: the chest's per-tick update will animate the lid down
            // and emit the "doorClose" sound on completion.
            chest.frameCounter.Value = -1;
        }
    }

    private void MarkDepositTripUndelivered(DepositTrip trip)
    {
        RouteUndeliveredTripStacks(trip, trip.Items);

        ModEntry.ModMonitor.Log(
            $"[Dayswork][deposit] could not reach deposit destination at ({trip.Tile.X},{trip.Tile.Y}); routed {trip.Items.Count} undelivered stack(s).",
            LogLevel.Warn);
    }

    private void DepositIntoChest(Chest chest, RoutedItemStack stack)
    {
        if (string.IsNullOrWhiteSpace(stack.QualifiedItemId))
        {
            ModEntry.ModMonitor.Log(
                $"[Dayswork][deposit] Error Item suppressed at chest: empty QualifiedItemId stack=x{stack.Quantity} task={stack.SourceTask}; not deposited.",
                LogLevel.Error);
            return;
        }
        var item = ItemRegistry.Create(stack.QualifiedItemId, stack.Quantity);
        if (item is null)
            return;
        if (IsDepositErrorItem(item))
        {
            ModEntry.ModMonitor.Log(
                $"[Dayswork][deposit] Error Item suppressed at chest: rawId='{stack.QualifiedItemId}' resolvedQualifiedId='{item.QualifiedItemId}' name='{item.Name}' stack=x{stack.Quantity} task={stack.SourceTask}; not deposited.",
                LogLevel.Error);
            return;
        }

        // addItem returns the remainder that did not fit (null if all fit).
        var leftover = chest.addItem(item);
        if (leftover is not null && leftover.Stack > 0)
        {
            // Chest full (FR-OUT-02): route the remainder to automatic overflow.
            _ctx!.Overflow.Add(new OverflowItem(
                new RoutedItemStack(stack.QualifiedItemId, leftover.Stack, stack.SourceTask, stack.Provenance),
                OverflowReason.ChestFull));
            ModEntry.ModMonitor.Log(
                $"[Dayswork][deposit] chest full; {leftover.Stack}x {stack.QualifiedItemId} routed to automatic overflow.",
                LogLevel.Trace);
        }
    }

    private void BeginExit(Farm farm)
    {
        _ctx!.StateMachine.Transition(ShiftPhase.Exiting, new IntentExitFarm());
        _nav.StartNavigation(_farmExitTile, farm, _farmhand!);
    }

    private void HandleExit(Farm farm)
    {
        // Wait for pathfinding navigation to the exit warp tile to finish (arrived or failed).
        // NavigationFailed is treated as "close enough" — remove the worker rather than walking
        // in a straight line through obstacles.
        if (!_nav.NavigationFailed && !_nav.HasArrived)
            return;

        _toolAnimator.StopSwing();

        if (_nav.NavigationFailed)
            ModEntry.ModMonitor.Log(
                $"[Dayswork][exit] could not path to exit tile ({_farmExitTile.X},{_farmExitTile.Y}) — removing worker in place.",
                LogLevel.Warn);
        else
            ModEntry.ModMonitor.Log("[Dayswork][exit] worker reached farm exit — shift complete.", LogLevel.Trace);

        ModEntry.ModMonitor.Log(
            $"[Dayswork] Shift complete. StopReason={_ctx!.StateMachine.StopReason}. Remaining stamina={_ctx.EnergyState.RemainingEnergy}/{_ctx.EnergyState.Capacity}.",
            LogLevel.Trace);

        // One settlement letter next morning for overflow items only; U-21 removes refund settlement.
        DispatchShiftOverflow();

        // The worker has finished and left for the day — light the office windows/lantern and
        // start the chimney smoke (gated by the Bindicle.Dayswork_WORKER_DONE GameStateQuery).
        // Reset next morning on DayStarted (ModEntry).
        HiringBuilding.WorkCompletedToday = true;

        ClearWorker();
        _ctx.StateMachine.Transition(ShiftPhase.Done);
        _ctx = null;
    }

    private void AdvanceWorkList(GameLocation location)
    {
        _stuck.Reset(); // any advance = progress signal
        _currentTileWork = null;
        RecordActiveBatchProgress();
        StartNextAnimalOrTileOrAdvance();
    }

    private void BeginDeposit()
    {
        if (_ctx is null)
            return;

        if (_ctx.StateMachine.Phase is ShiftPhase.Depositing or ShiftPhase.Exiting or ShiftPhase.Done)
            return;

        var farm = Game1.getFarm();
        ReturnWorkerToFarmForDeposit();
        // Valid from Working, Stuck, Recovering (all have Depositing as a successor).
        _morningEntranceHoldTicks = 0;
        if (_pendingDebrisSweeps.Count > 0)
        {
            _waitingForDebrisBeforeDeposit = true;
            _actionPending = true;
            _toolAnimator.StopSwing();
//             ModEntry.ModMonitor.Log(
//                 $"[Dayswork][debris] waiting for {_pendingDebrisSweeps.Count} pending debris sweep(s) before deposit.",
//                 LogLevel.Trace);
            return;
        }

        FlushPendingDebrisSweeps();

        // Plan the deposit run from the task-tagged buffer (Pattern M).
        var workerTile = _farmhand is not null
            ? new TileCoord(_farmhand.TilePoint.X, _farmhand.TilePoint.Y)
            : _farmExitTile;
        var plan = _depositPlanner.Plan(
            _ctx!.Buffer.Snapshot(),
            _ctx.TaskDestinations,
            ResolveShippingBinDepositTile(farm),
            workerTile,
            Manhattan);

        // Items resolved straight to automatic delivery are seeded into the overflow set (Pattern O / FD-Q2=A).
        foreach (var stack in plan.AutomaticOverflow)
            _ctx.Overflow.Add(new OverflowItem(stack, OverflowReason.NoChestAssigned));

        // The buffer is now consumed into the plan; clear it so nothing is double-counted.
        _ctx.Buffer.TakeAll();

        _depositTrips.Clear();
        foreach (var trip in plan.Trips)
            _depositTrips.Enqueue(trip);
        _currentTrip = null;

        // Enter Depositing. With no walkable trips, pass straight through to Exiting (Pattern N).
        var stopReason = _ctx.PendingStopReason ?? ShiftStopReason.Completed;
        _ctx.PendingStopReason = null;
        if (_depositTrips.Count == 0)
        {
            _ctx.StateMachine.BeginWrapUp(new IntentDepositInShippingBin(), stopReason);
            BeginExit(farm);
            return;
        }

        var first = _depositTrips.Dequeue();
        _currentTrip = first;
        _ctx.StateMachine.BeginWrapUp(ToDepositIntent(first), stopReason);
        StartDepositTrip(first);
    }

    private void StartDepositTrip(DepositTrip trip)
    {
        if (_farmhand is null)
            return;

        var farm = Game1.getFarm();
        if (trip.Destination is ChestDestination { Ref.LocationName: not "Farm" } chestDest)
        {
            if (ModEntry.ExpansionCompat is { } compat &&
                compat.IsExpansionDepositLocation(chestDest.Ref.LocationName))
            {
                var source = (_currentLocation ?? farm).NameOrUniqueName;
                if (TryStartExpansionRoute(
                        source,
                        chestDest.Ref.LocationName,
                        ExpansionRoutePurpose.DepositEntry,
                        PendingExpansionRouteKind.DepositEntry,
                        batch: null))
                    return;

                MarkDepositTripUndelivered(trip);
                FinalizeAndAdvanceTrip(farm);
                return;
            }

            if (_buildingNavigator.TryResolveDoorTile(chestDest.Ref.LocationName, out var outdoorDoor, out var interior))
            {
                // Walk across the farm to the building's outdoor door first; we only cross the
                // door (warp into the interior) once the worker arrives there — see HandleDeposit.
                _pendingDepositInterior = interior;
                _currentLocation = farm;
                _nav.StartNavigation(outdoorDoor, farm, _farmhand);
                return;
            }

            foreach (var stack in trip.Items)
                _ctx!.Overflow.Add(new OverflowItem(stack, OverflowReason.ChestMissing));
            _currentTrip = null;
            return;
        }

        _currentLocation = farm;
        if (trip.Destination is ChestDestination farmChest)
        {
            StartChestDepositNavigation(trip, farmChest, farm);
            return;
        }

        _nav.StartNavigation(trip.Tile, farm, _farmhand);
    }

    private void EnterDepositInterior(GameLocation interior)
    {
        _pendingDepositInterior = null;
        if (_farmhand is null || _currentTrip is not { Destination: ChestDestination chestDest })
            return;

        var entryTile = _buildingNavigator.ResolveInteriorEntryTile(interior);
        _buildingNavigator.Enter(_farmhand, interior, entryTile);
        _currentLocation = interior;
        StartChestDepositNavigation(_currentTrip, chestDest, interior);
    }

    private void StartChestDepositNavigation(DepositTrip trip, ChestDestination chestDest, GameLocation location)
    {
        if (_farmhand is null)
            return;

        if (TrySelectChestDepositStandTile(chestDest.Ref.Tile, location, _farmhand, out var standTile))
        {
            _nav.StartNavigation(standTile, location, _farmhand);
            return;
        }

        MarkDepositTripUndelivered(trip);
        _currentTrip = null;
    }

    private static bool TrySelectChestDepositStandTile(
        TileCoord chestTile,
        GameLocation location,
        FarmhandNpc worker,
        out TileCoord standTile)
    {
        var source = new TileCoord(worker.TilePoint.X, worker.TilePoint.Y);
        var routeCosts = WorkerMovementDriver.ComputeRouteCostsFrom(source, location);
        return WorkerRouteSelector.TrySelectNearestReachableTile(
            DepositStandTilesAround(chestTile),
            routeCosts,
            out standTile);
    }

    private static IEnumerable<TileCoord> DepositStandTilesAround(TileCoord tile)
    {
        yield return new TileCoord(tile.X, tile.Y - 1);
        yield return new TileCoord(tile.X + 1, tile.Y);
        yield return new TileCoord(tile.X, tile.Y + 1);
        yield return new TileCoord(tile.X - 1, tile.Y);
    }

    private void CompleteDepositTripLocation(DepositTrip trip)
    {
        if (_farmhand is null ||
            trip.Destination is not ChestDestination { Ref.LocationName: not "Farm" } chestDest)
            return;

        if (ModEntry.ExpansionCompat is { } compat &&
            compat.IsExpansionDepositLocation(chestDest.Ref.LocationName))
            return;

        if (_buildingNavigator.TryResolveDoorTile(chestDest.Ref.LocationName, out var outdoorDoor, out _))
        {
            _buildingNavigator.ExitToFarm(_farmhand, outdoorDoor);
            _currentLocation = Game1.getFarm();
        }
    }

    private void ReturnWorkerToFarmForDeposit()
    {
        if (_farmhand is null)
            return;

        var farm = Game1.getFarm();
        if ((_farmhand.currentLocation ?? farm) == farm)
        {
            _currentLocation = farm;
            return;
        }

        var currentLocation = _farmhand.currentLocation ?? _currentLocation;
        if (currentLocation is not null &&
            ModEntry.ExpansionCompat is { } compat &&
            compat.IsExpansionDepositLocation(currentLocation.NameOrUniqueName))
        {
            if (compat.TryValidateRoute(
                    farm,
                    currentLocation.NameOrUniqueName,
                    "Farm",
                    ExpansionRoutePurpose.ReturnToFarm,
                    out var route,
                    out var failure))
            {
                var farmArrival = route.Hops[^1].Hop.ArrivalTile;
                _nav.WarpWorker(_farmhand, currentLocation, farm, farmArrival);
                _currentLocation = farm;
                return;
            }

            LogExpansionRouteFailure(failure);
            WarpExpansionWorkerToFarm();
            return;
        }

        var batch = _ctx is not null && _ctx.CurrentBatchIndex < _ctx.Batches.Count
            ? _ctx.Batches[_ctx.CurrentBatchIndex]
            : null;

        var exitTile = batch is not null &&
                       BatchRequiresInteriorEntry(batch) &&
                       _buildingNavigator.TryResolveDoorTile(batch.LocationName, out var outdoorDoor, out _)
            ? outdoorDoor
            : _farmExitTile;

        _buildingNavigator.ExitToFarm(_farmhand, exitTile);
        _currentLocation = farm;
    }

    private static bool BatchRequiresInteriorEntry(WorkBatch batch) =>
        batch.Kind is BatchKind.AnimalBuilding or BatchKind.Greenhouse;

    private void DispatchShiftOverflow()
    {
        if (_ctx is null) return;

        IReadOnlyList<ItemStack> items = _ctx.Overflow.Count > 0
            ? ConsolidateOverflow(_ctx.Overflow)
            : Array.Empty<ItemStack>();
        var categories = _overflowCategorizer.Categorize(_ctx.Overflow);

        _shiftOutcomeDispatcher.DispatchOverflowDelivery(items, categories);
        _ctx.Overflow.Clear();
    }

    private void AppendUndeliveredToOverflow()
    {
        if (_ctx is null) return;

        foreach (var b in _ctx.Buffer.TakeAll())
        {
            var stack = new RoutedItemStack(b.QualifiedItemId, b.Quantity, b.SourceTask, b.Provenance);
            if (DepositFallbackPolicy.ResolveUndelivered(ResolveAssignedDestination(b.SourceTask, _ctx.TaskDestinations))
                == UndeliveredDepositResolution.ShippingBin)
                DepositIntoShippingBin(stack, animateWhenPlayerHere: false);
            else
                _ctx.Overflow.Add(new OverflowItem(stack, OverflowReason.NotDelivered));
        }

        if (_currentTrip is not null)
        {
            RouteUndeliveredTripStacks(
                _currentTrip,
                _currentTrip.Items.Skip(Math.Clamp(_currentTripStackIndex, 0, _currentTrip.Items.Count)));
            _currentTrip = null;
        }

        while (_depositTrips.Count > 0)
        {
            var trip = _depositTrips.Dequeue();
            RouteUndeliveredTripStacks(trip, trip.Items);
        }
    }

    private void RouteUndeliveredTripStacks(DepositTrip trip, IEnumerable<RoutedItemStack> stacks)
    {
        if (_ctx is null) return;

        if (DepositFallbackPolicy.ResolveUndelivered(trip.Destination) == UndeliveredDepositResolution.ShippingBin)
        {
            foreach (var stack in stacks)
                DepositIntoShippingBin(stack, animateWhenPlayerHere: false);
            return;
        }

        foreach (var stack in stacks)
            _ctx.Overflow.Add(new OverflowItem(stack, OverflowReason.NotDelivered));
    }
}
