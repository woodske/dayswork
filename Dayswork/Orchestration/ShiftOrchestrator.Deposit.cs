using Dayswork.Core.Config;
using Dayswork.Compat;
using Dayswork.Core.Compat;
using Dayswork.Core.Crops;
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
    internal void BeginExit(Farm farm)
    {
        Session.Ctx.StateMachine.Transition(ShiftPhase.Exiting, new IntentExitFarm());
        Session.CurrentExitTile = ResolveReachableShiftExitTile(farm);
        Session.PendingNavTile = Session.CurrentExitTile;
        Session.PendingTaskTile = Session.CurrentExitTile;
        _nav.StartNavigation(Session.CurrentExitTile, farm, Session.Worker!);
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
                $"[Dayswork][exit] could not path to exit tile ({Session.CurrentExitTile.X},{Session.CurrentExitTile.Y}) — removing worker in place.",
                DevLog.WarnLevel);
        else
            ModEntry.ModMonitor.Log("[Dayswork][exit] worker reached farm exit — shift complete.", LogLevel.Trace);

        ModEntry.ModMonitor.Log(
            $"[Dayswork] Shift complete. StopReason={Session.Ctx.StateMachine.StopReason}. Remaining stamina={Session.Ctx.EnergyState.RemainingEnergy}/{Session.Ctx.EnergyState.Capacity}.",
            LogLevel.Trace);

        // One settlement letter next morning for overflow items only; refunds are not settled here.
        DispatchShiftOverflow();

        // The worker has finished and left for the day — light the office windows/lantern and
        // start the chimney smoke (gated by the Bindicle.Dayswork_WORKER_DONE GameStateQuery).
        // Reset next morning on DayStarted (ModEntry).
        HiringBuilding.WorkCompletedToday = true;

        var session = Session;
        DespawnWorker();
        session.Ctx.StateMachine.Transition(ShiftPhase.Done);
        _session = null;
    }

    private void AdvanceWorkList(GameLocation location)
    {
        Session.Stuck.Reset(); // any advance = progress signal
        Session.CurrentTileWork = null;
        RecordActiveBatchProgress();
        StartNextAnimalOrTileOrAdvance();
    }

    private void BeginDeposit()
    {
        if (_session is null)
            return;

        if (Session.Ctx.StateMachine.Phase is ShiftPhase.Depositing or ShiftPhase.Exiting or ShiftPhase.Done)
            return;

        // A wrap-up (energy cap, 8pm, cancel, stuck-home) can fire mid-travel; stop the runner
        // before the safety warp below repositions the worker.
        CancelActiveTravel();

        // If the worker is inside a building, walk them to the door first so they always exit
        // on foot rather than warping. BeginDeposit is called again on ExitForDeposit arrival.
        if (TryStartExitTravelForDeposit())
            return;

        var farm = Game1.getFarm();
        ReturnWorkerToFarmForDeposit();
        // Valid from Working, Stuck, Recovering (all have Depositing as a successor).
        Session.MorningEntranceHoldTicks = 0;
        if (Session.PendingDebrisSweeps.Count > 0)
        {
            Session.WaitingForDebrisBeforeDeposit = true;
            Session.ActionPending = true;
            _toolAnimator.StopSwing();
            return;
        }

        FlushPendingDebrisSweeps();

        // Return any inputs still in the machine carry buffer (mid-reload wrap-up) before depositing.
        SettleCarriedInputs();

        // Plan the deposit run from the task-tagged buffer.
        var workerTile = Session.Worker is not null
            ? new TileCoord(Session.Worker.TilePoint.X, Session.Worker.TilePoint.Y)
            : Session.FarmExitTile;
        var plan = _depositPlanner.Plan(
            Session.Ctx.Buffer.Snapshot(),
            Session.Ctx.TaskDestinations,
            BuildOutputProvenanceMap(),
            ResolveShippingBinDepositTile(farm),
            workerTile,
            Manhattan);

        // Items resolved straight to automatic delivery are seeded into the overflow set.
        foreach (var stack in plan.AutomaticOverflow)
            Session.Ctx.Overflow.Add(new OverflowItem(stack, OverflowReason.NoChestAssigned));

        // The buffer is now consumed into the plan; clear it so nothing is double-counted.
        Session.Ctx.Buffer.TakeAll();

        Session.Deposits.Load(plan.Trips);

        // Enter Depositing. With no walkable trips, pass straight through to Exiting.
        var stopReason = Session.Ctx.PendingStopReason ?? ShiftStopReason.Completed;
        Session.Ctx.PendingStopReason = null;
        if (!Session.Deposits.HasPending)
        {
            Session.Ctx.StateMachine.BeginWrapUp(new IntentDepositInShippingBin(), stopReason);
            BeginExit(farm);
            return;
        }

        var first = Session.Deposits.BeginNextTrip();
        Session.Ctx.StateMachine.BeginWrapUp(ToDepositIntent(first), stopReason);
        Session.Deposits.StartTravelToTrip(first);
    }

    internal static bool TrySelectChestDepositStandTile(
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

    internal static IEnumerable<TileCoord> DepositStandTilesAround(TileCoord tile)
    {
        yield return new TileCoord(tile.X, tile.Y - 1);
        yield return new TileCoord(tile.X + 1, tile.Y);
        yield return new TileCoord(tile.X, tile.Y + 1);
        yield return new TileCoord(tile.X - 1, tile.Y);
    }

    private bool TryStartExitTravelForDeposit()
    {
        if (Session.Worker is null)
            return false;

        var farm = Game1.getFarm();
        var currentLocation = Session.Worker.currentLocation ?? Session.CurrentLocation;
        if (currentLocation is null || currentLocation == farm)
            return false;

        // Expansion greenhouse: hop home along the validated route.
        if (ModEntry.ExpansionCompat is { } compat &&
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
                StartTravel(BuildExpansionPlan(route, TravelFailurePolicy.WarpToDestination),
                    TravelPurpose.ExitForDeposit);
                return true;
            }
            LogExpansionRouteFailure(failure);
            return false;
        }

        // Vanilla building interior: walk to the door, warp out.
        // If the door can't be resolved, fall through to the warp in ReturnWorkerToFarmForDeposit.
        if (!_buildingNavigator.TryResolveDoorTile(currentLocation.NameOrUniqueName, out var outdoorDoor, out _))
            return false;

        StartTravel(BuildBuildingExitPlan(currentLocation, outdoorDoor), TravelPurpose.ExitForDeposit);
        return true;
    }

    private void ReturnWorkerToFarmForDeposit()
    {
        if (Session.Worker is null)
            return;

        var farm = Game1.getFarm();
        if ((Session.Worker.currentLocation ?? farm) == farm)
        {
            Session.CurrentLocation = farm;
            return;
        }

        var currentLocation = Session.Worker.currentLocation ?? Session.CurrentLocation;
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
                _nav.WarpWorker(Session.Worker, currentLocation, farm, farmArrival);
                Session.CurrentLocation = farm;
                return;
            }

            LogExpansionRouteFailure(failure);
            WarpExpansionWorkerToFarm();
            return;
        }

        var batch = _session is not null && Session.Ctx.CurrentBatchIndex < Session.Ctx.Batches.Count
            ? Session.Ctx.Batches[Session.Ctx.CurrentBatchIndex]
            : null;

        var exitTile = batch is not null &&
                       BatchRequiresInteriorEntry(batch) &&
                       _buildingNavigator.TryResolveDoorTile(batch.LocationName, out var outdoorDoor, out _)
            ? outdoorDoor
            : Session.FarmExitTile;

        _nav.WarpWorker(Session.Worker, Session.Worker.currentLocation ?? farm, farm, exitTile);
        Session.CurrentLocation = farm;
    }

    private static bool BatchRequiresInteriorEntry(WorkBatch batch) =>
        batch.Kind is BatchKind.AnimalBuilding or BatchKind.Greenhouse;

    // Merges the per-group output destinations for managed crops and machines into one
    // provenance → destination map for the deposit planner. Machine ids never collide with crop
    // assignment keys (different OutputScopeFamily), so a simple union is safe.
    private IReadOnlyDictionary<OutputScopeProvenance, DestinationKey> BuildOutputProvenanceMap()
    {
        var map = new Dictionary<OutputScopeProvenance, DestinationKey>(
            ManagedCropOutputRouter.BuildDestinationMap(Session.Ctx.WorkScopes.ManagedCrops?.Assignments));

        foreach (var (provenance, destination) in Dayswork.Core.Machines.MachineOutputRouter.BuildDestinationMap(
                     Session.Ctx.WorkScopes.Machines?.Groups))
            map[provenance] = destination;

        return map;
    }

    private void DispatchShiftOverflow()
    {
        if (_session is null) return;

        IReadOnlyList<ItemStack> items = Session.Ctx.Overflow.Count > 0
            ? ConsolidateOverflow(Session.Ctx.Overflow)
            : Array.Empty<ItemStack>();
        var categories = _overflowCategorizer.Categorize(Session.Ctx.Overflow);

        _shiftOutcomeDispatcher.DispatchOverflowDelivery(items, categories);
        Session.Ctx.Overflow.Clear();
    }
}
