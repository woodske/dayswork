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

        // Guard: in a resume deposit mode (pre-idle flush or eager mid-batch flush), don't re-plan once
        // trips are already executing. Use IsActive (not intent) because StartTravel overwrites the
        // intent with IntentMoveToTile during deposit trip travel, which would make an intent check
        // silently fail. Any pending stop reason (energy/8pm) is honoured in the resume callback.
        if (Session.DepositResume != DepositResumeMode.None && Session.Deposits.IsActive)
            return;

        // If the 8pm cap (or any external wrap-up trigger) fires while the worker is in the
        // idle wait loop, IdleWaiting is still true at this point. Clear it so the deposit
        // loop isn't blocked — the tick handler short-circuits on IdleWaiting=true and the
        // deposit intent dispatch would never run otherwise.
        Session.IdleWaiting = false;

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
        // Eager mid-batch flush deposits only player-chest-bound items; the terminal/pre-idle flush
        // takes everything.
        var eager = Session.DepositResume == DepositResumeMode.ResumeBatch;
        var workerLocation = Session.Worker?.currentLocation?.NameOrUniqueName ?? farm.Name;
        var plan = _depositPlanner.Plan(
            Session.Ctx.Buffer.Snapshot(),
            Session.Ctx.TaskDestinations,
            BuildOutputProvenanceMap(),
            new DepositStop(farm.Name, ResolveShippingBinDepositTile(farm)),
            new DepositStop(workerLocation, workerTile),
            (a, b) => DepositStopDistance(a, b, farm),
            chestDestinationsOnly: eager);

        // Items resolved straight to automatic delivery are seeded into the overflow set.
        // (Empty for an eager plan — non-chest items are retained in the buffer instead.)
        foreach (var stack in plan.AutomaticOverflow)
            Session.Ctx.Overflow.Add(new OverflowItem(stack, OverflowReason.NoChestAssigned));

        // The buffer is now consumed into the plan; clear it so nothing is double-counted.
        Session.Ctx.Buffer.TakeAll();

        // Eager plans hold back everything not bound for a player chest (shipping bin / office output);
        // put it back in the buffer to ride along to the terminal deposit.
        foreach (var stack in plan.Retained)
            Session.Ctx.Buffer.Add(stack.QualifiedItemId, stack.Quantity, stack.SourceTask, stack.Provenance, stack.Quality, stack.FlavorId);

        Session.Deposits.Load(plan.Trips);

        if (eager)
        {
            // Mid-shift chest flush: stay in Working phase and resume the next batch when done.
            if (!Session.Deposits.HasPending)
            {
                Session.DepositResume = DepositResumeMode.None;
                BeginCurrentBatch();
                return;
            }
            var firstBatchTrip = Session.Deposits.BeginNextTrip();
            Session.Ctx.StateMachine.SetIntent(ToDepositIntent(firstBatchTrip));
            Session.Deposits.StartTravelToTrip(firstBatchTrip);
            return;
        }

        if (Session.DepositResume == DepositResumeMode.ResumeIdle)
        {
            // Pre-idle path: stay in Working phase so we can return to idle after depositing.
            if (!Session.Deposits.HasPending)
            {
                Session.DepositResume = DepositResumeMode.None;
                if (ShouldWrapUpBeforeNextUnit())
                {
                    QueueWrapUpNow(Session.Ctx.PendingStopReason ?? ShiftStopReason.Completed);
                    return;
                }
                BeginIdleWait();
                return;
            }
            var firstIdleTrip = Session.Deposits.BeginNextTrip();
            Session.Ctx.StateMachine.SetIntent(ToDepositIntent(firstIdleTrip));
            Session.Deposits.StartTravelToTrip(firstIdleTrip);
            return;
        }

        // Normal terminal deposit: transition to Depositing and head home.
        var stopReason = Session.Ctx.PendingStopReason ?? ShiftStopReason.Completed;
        Session.Ctx.PendingStopReason = null;
        if (stopReason == ShiftStopReason.Exhausted)
            Game1.addHUDMessage(new HUDMessage(I18nHelper.Get("notify.farmhand_exhausted"), HUDMessage.newQuest_type));
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

    /// <summary>
    /// Eager mid-shift deposit hook, called at each work-batch boundary. If the feature is enabled and
    /// the buffer holds items bound for a player-assigned chest, runs a chest-only deposit run and
    /// resumes the next batch when it finishes (see <see cref="OnBatchDepositComplete"/>). Returns true
    /// when a deposit run was started (the caller must return). Shipping-bin / office-output items are
    /// left in the buffer for the terminal deposit, so this never detours for a terminal-sink-only haul.
    /// </summary>
    private bool TryBeginEagerChestDeposit()
    {
        if (_session is null || Session.Worker is null)
            return false;

        if (!_config.EagerChestDeposits)
            return false;

        // Don't detour if a resume deposit is already in flight or the shift is winding down.
        if (Session.DepositResume != DepositResumeMode.None)
            return false;

        if (ShouldWrapUpBeforeNextUnit())
            return false;

        if (Session.Ctx.Buffer.IsEmpty)
            return false;

        if (!DepositPlanner.HasChestDestination(
                Session.Ctx.Buffer.Snapshot(),
                Session.Ctx.TaskDestinations,
                BuildOutputProvenanceMap()))
            return false;

        Session.DepositResume = DepositResumeMode.ResumeBatch;
        BeginDeposit();
        return true;
    }

    /// <summary>
    /// Called by <see cref="DepositTripRunner"/> when an eager mid-batch deposit run finishes. Honours
    /// a stop reason that fired mid-deposit; otherwise resumes the work plan at the current batch.
    /// </summary>
    internal void OnBatchDepositComplete()
    {
        if (_session is null)
            return;

        Session.DepositResume = DepositResumeMode.None;
        if (ShouldWrapUpBeforeNextUnit())
        {
            QueueWrapUpNow(Session.Ctx.PendingStopReason ?? ShiftStopReason.Completed);
            return;
        }

        BeginCurrentBatch();
    }

    // Cross-location deposit-trip metric (architecture review #6). Same-location stops compare by
    // tile (Manhattan); cross-location stops measure between the buildings' farm-side door tiles plus
    // a hop penalty far larger than any intra-location distance — so same-location chests always group
    // and the worker never zig-zags farm → interior → farm. Locations that don't resolve to a
    // farm-side door (e.g. an expansion without a farm warp) sort last via a large sentinel, the
    // right degradation.
    private const int DepositHopCost = 10_000;
    private const int DepositUnresolvedPenalty = 1_000_000;

    private int DepositStopDistance(DepositStop a, DepositStop b, Farm farm)
    {
        if (string.Equals(a.LocationName, b.LocationName, StringComparison.Ordinal))
            return Manhattan(a.Tile, b.Tile);

        if (!TryFarmSideTile(a, farm, out var farmA, out var interiorA) ||
            !TryFarmSideTile(b, farm, out var farmB, out var interiorB))
            return DepositUnresolvedPenalty;

        // farm ↔ interior = 1 hop; interior ↔ interior (different) = 2 hops via the farm.
        var hops = interiorA && interiorB ? 2 : 1;
        return hops * DepositHopCost + Manhattan(farmA, farmB);
    }

    private static bool TryFarmSideTile(DepositStop stop, Farm farm, out TileCoord tile, out bool isInterior)
    {
        if (string.Equals(stop.LocationName, farm.Name, StringComparison.Ordinal))
        {
            tile = stop.Tile;
            isInterior = false;
            return true;
        }

        if (BuildingLocationResolver.TryResolve(farm, stop.LocationName, out var match))
        {
            tile = match.OutdoorDoorTile;
            isInterior = true;
            return true;
        }

        tile = default;
        isInterior = false;
        return false;
    }

    internal static bool TrySelectChestDepositStandTile(
        TileCoord chestTile,
        GameLocation location,
        FarmhandNpc worker,
        LocationPassabilityCache passability,
        out TileCoord standTile)
    {
        var source = new TileCoord(worker.TilePoint.X, worker.TilePoint.Y);
        var routeCosts = passability.RouteCostsFrom(source, location);
        return WorkerRouteSelector.TrySelectPreferredStandTile(
            DepositStandTilesAround(chestTile),
            routeCosts,
            out standTile);
    }

    // Covers chest/bin deposits and machine collect/reload (machines route here via
    // ResolveMachineNavTile → TrySelectChestDepositStandTile). Cardinals first, then diagonals: the
    // player can interact diagonally too, and the route selector is first-wins-on-ties, so a diagonal
    // is only chosen when it's strictly closer or the only reachable tile.
    internal static IEnumerable<StandTile> DepositStandTilesAround(TileCoord tile)
    {
        yield return new StandTile(new TileCoord(tile.X, tile.Y - 1), false);
        yield return new StandTile(new TileCoord(tile.X + 1, tile.Y), false);
        yield return new StandTile(new TileCoord(tile.X, tile.Y + 1), false);
        yield return new StandTile(new TileCoord(tile.X - 1, tile.Y), false);
        yield return new StandTile(new TileCoord(tile.X - 1, tile.Y - 1), true);
        yield return new StandTile(new TileCoord(tile.X + 1, tile.Y - 1), true);
        yield return new StandTile(new TileCoord(tile.X - 1, tile.Y + 1), true);
        yield return new StandTile(new TileCoord(tile.X + 1, tile.Y + 1), true);
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

        foreach (var (provenance, destination) in Dayswork.Core.FishPonds.FishPondOutputRouter.BuildDestinationMap(
                     Session.Ctx.WorkScopes.FishPonds))
            map[provenance] = destination;

        return map;
    }

    private void DispatchShiftOverflow()
    {
        if (_session is null) return;

        // Dev tripwire summary: if vanilla mis-routed any worker loot into the player's location
        // this shift, report the recovered/stranded tally. Stranded > 0 means loot escaped the
        // recovery sweep and should be investigated (see docs/debris-and-drops.md).
        if (DevLog.Enabled && (Session.LeakBeatsObserved > 0 || Session.LeakItemsStranded > 0))
            LogLeakAudit(Session.LeakItemsStranded > 0 ? LogLevel.Warn : LogLevel.Info);

        IReadOnlyList<ItemStack> items = Session.Ctx.Overflow.Count > 0
            ? ConsolidateOverflow(Session.Ctx.Overflow)
            : Array.Empty<ItemStack>();
        var categories = _overflowCategorizer.Categorize(Session.Ctx.Overflow);

        _shiftOutcomeDispatcher.DispatchOverflowDelivery(items, categories, Session.Flavors.Templates);
        Session.Ctx.Overflow.Clear();
    }
}
