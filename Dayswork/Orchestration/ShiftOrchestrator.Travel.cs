using Dayswork.Compat;
using Dayswork.Core.Compat;
using Dayswork.Core.Domain;
using Dayswork.Core.Shifts;
using Dayswork.Integration;
using Dayswork.Worker;
using StardewModdingAPI;
using StardewValley;

namespace Dayswork.Orchestration;

internal sealed partial class ShiftOrchestrator
{
    /// <summary>What the orchestrator does when the active travel completes (or fails).</summary>
    private enum TravelPurpose
    {
        None,
        WorkEntry,       // into the active batch's work location
        WorkExit,        // back to the farm after a batch (batch index already advanced)
        DepositEntry,    // to the active deposit trip's chest location
        DepositExit,     // back to the farm after an interior deposit
        ShoppingStep,    // managed shopping; sub-dispatched on _managedShoppingPhase
        ManagedReentry,  // back into the managed-crop field location after shopping
    }

    private TravelPurpose _travelPurpose;

    private void StartTravel(TravelPlan plan, TravelPurpose purpose)
    {
        if (_farmhand is null)
            return;

        _toolAnimator.StopSwing();
        _travelPurpose = purpose;

        if (plan.Legs.Count > 0)
        {
            var firstLeg = plan.Legs[0];
            _pendingNavTile = firstLeg.WalkTarget;
            _pendingTaskTile = firstLeg.WalkTarget;
            EnsureWorkingIntent(new IntentMoveToTile(firstLeg.WalkTarget));
            _currentLocation = firstLeg.WalkLocation;
        }

        DevLog.Log(
            $"[Dayswork][travel] start purpose={purpose} legs={plan.Legs.Count} policy={plan.OnFailure} " +
            $"destination={DescribeTravelDestination(plan)}.");
        _travel.Start(plan, _farmhand);
    }

    private void HandleTravel()
    {
        _travel.Update();
        if (_farmhand?.currentLocation is { } location)
            _currentLocation = location;

        if (_travel.Failed)
        {
            var purpose = _travelPurpose;
            var detail = _travel.FailureDetail;
            CancelActiveTravel();
            OnTravelFailed(purpose, detail);
            return;
        }

        if (!_travel.IsComplete)
            return;

        var arrived = _travelPurpose;
        if (_travel.CompletedWithForcedWarp)
            DevLog.Log(
                $"[Dayswork][travel] purpose={arrived} forced warp to destination ({_travel.FailureDetail}).",
                LogLevel.Warn);
        CancelActiveTravel();
        OnTravelArrived(arrived);
    }

    private void CancelActiveTravel()
    {
        _travel.Clear();
        _travelPurpose = TravelPurpose.None;
    }

    private void OnTravelArrived(TravelPurpose purpose)
    {
        switch (purpose)
        {
            case TravelPurpose.WorkEntry:
                CompleteWorkEntryTravel();
                break;
            case TravelPurpose.WorkExit:
                CompleteWorkExitTravel();
                break;
            case TravelPurpose.ManagedReentry:
                ResumeManagedBatchAfterShopping();
                break;
            case TravelPurpose.DepositEntry:
                CompleteDepositEntryTravel();
                break;
            case TravelPurpose.DepositExit:
                FinalizeAndAdvanceTrip(Game1.getFarm());
                break;
            case TravelPurpose.ShoppingStep:
                OnManagedShoppingTravelArrived();
                break;
        }
    }

    private void OnTravelFailed(TravelPurpose purpose, string? detail)
    {
        DevLog.Log($"[Dayswork][travel] purpose={purpose} failed: {detail}.", LogLevel.Warn);
        switch (purpose)
        {
            case TravelPurpose.WorkEntry:
                SkipCurrentBatchAfterEntryFailure();
                break;
            case TravelPurpose.DepositEntry:
                if (_currentTrip is not null)
                    MarkDepositTripUndelivered(_currentTrip);
                FinalizeAndAdvanceTrip(Game1.getFarm());
                break;
            case TravelPurpose.ShoppingStep:
                AbortManagedShoppingTrip($"navigation_failed_{_managedShoppingPhase}");
                break;
        }
    }

    // ---- plan builders ----

    /// <summary>Farm → building interior: walk to the outdoor door, warp to the interior entry tile.</summary>
    private bool TryBuildBuildingEntryPlan(
        string interiorLocationName,
        TravelFailurePolicy policy,
        out TravelPlan plan)
    {
        plan = null!;
        if (!_buildingNavigator.TryResolveDoorTile(interiorLocationName, out var outdoorDoor, out var interior))
            return false;

        var entryTile = _buildingNavigator.ResolveInteriorEntryTile(interior);
        plan = new TravelPlan(
            new[] { new TravelLeg(Game1.getFarm(), outdoorDoor, interior, entryTile) },
            policy);
        return true;
    }

    /// <summary>Building interior → farm: walk to the interior exit door, warp out to the farm.</summary>
    private TravelPlan BuildBuildingExitPlan(GameLocation interior, TileCoord farmArrivalTile)
    {
        var exitCandidates = _buildingNavigator.ResolveInteriorExitApproachTiles(interior);
        var source = new TileCoord(_farmhand!.TilePoint.X, _farmhand.TilePoint.Y);
        var routeCosts = WorkerMovementDriver.ComputeRouteCostsFrom(source, interior);
        var exitTile = BuildingWorkNavigator.SelectNearestReachableExitApproachTile(
            exitCandidates,
            routeCosts,
            exitCandidates[0]);
        return new TravelPlan(
            new[] { new TravelLeg(interior, exitTile, Game1.getFarm(), farmArrivalTile) },
            TravelFailurePolicy.WarpToDestination);
    }

    /// <summary>A walk within one location, no warp (counter walks, wait tiles, chest stand tiles).</summary>
    private static TravelPlan WalkOnlyPlan(
        GameLocation location,
        TileCoord tile,
        TravelFailurePolicy policy = TravelFailurePolicy.ReportFailure) =>
        new(new[] { new TravelLeg(location, tile, null, tile) }, policy);

    /// <summary>Multi-hop expansion route: each hop walks to its approach tile, then warps onward.</summary>
    private static TravelPlan BuildExpansionPlan(ValidatedExpansionRoute route, TravelFailurePolicy policy)
    {
        var legs = route.Hops
            .Select(hop => new TravelLeg(hop.Source, hop.Hop.ApproachTile, hop.Target, hop.Hop.ArrivalTile))
            .ToList();
        return new TravelPlan(legs, policy);
    }

    /// <summary>Validates an expansion route and starts travel along it. False when validation fails.</summary>
    private bool TryStartExpansionTravel(
        string sourceLocationName,
        string targetLocationName,
        ExpansionRoutePurpose routePurpose,
        TravelFailurePolicy policy,
        TravelPurpose travelPurpose)
    {
        if (_farmhand is null || ModEntry.ExpansionCompat is not { } compat)
            return false;

        if (!compat.TryValidateRoute(
                Game1.getFarm(),
                sourceLocationName,
                targetLocationName,
                routePurpose,
                out var route,
                out var failure))
        {
            LogExpansionRouteFailure(failure);
            return false;
        }

        StartTravel(BuildExpansionPlan(route, policy), travelPurpose);
        return true;
    }

    // ---- arrival / failure handlers ----

    /// <summary>The worker is inside the active batch's work location; scan it and start working.</summary>
    private void CompleteWorkEntryTravel()
    {
        if (_ctx is null || _farmhand is null)
            return;

        var batch = _ctx.Batches[_ctx.CurrentBatchIndex];
        var location = _farmhand.currentLocation ?? Game1.getLocationFromName(batch.LocationName);
        if (location is null)
        {
            _ctx.CurrentBatchIndex++;
            BeginCurrentBatch();
            return;
        }

        _currentLocation = location;
        ModEntry.ModMonitor.Log(
            I18nHelper.Get("log.building.entering", new { location = location.Name }),
            LogLevel.Trace);

        if (batch.Kind == BatchKind.ManagedCrops)
        {
            BeginManagedCropBatch(batch);
            return;
        }

        IReadOnlyList<WorkItem> tileWork;
        IReadOnlyList<AnimalWorkItem> animalWork;
        var batchTasks = batch.Tasks.ToHashSet();

        if (batch.Kind == BatchKind.AnimalBuilding)
        {
            tileWork = _indoorScanner.ScanInterior(
                location,
                batchTasks,
                _ctx.ToolSnapshot,
                OutputScopeProvenance.AnimalBuilding(batch.LocationName));
            if (batch.FeedBuilding)
            {
                _currentFeedPlan = _animalHandler.CreateFeedWork(location);
                tileWork = _currentFeedPlan.WorkItems.Concat(tileWork).ToList();
                _hayInHand = 0;
            }
            else
            {
                _currentFeedPlan = null;
                _hayInHand = 0;
            }

            var selectedHome = new HashSet<string>(StringComparer.Ordinal) { batch.LocationName };
            animalWork = BuildAnimalWork(location, selectedHome, batchTasks);
        }
        else
        {
            _currentFeedPlan = null;
            _hayInHand = 0;
            tileWork = _indoorScanner.ScanInterior(
                location,
                batchTasks,
                _ctx.ToolSnapshot,
                OutputScopeProvenance.Greenhouse(batch.LocationName));
            animalWork = Array.Empty<AnimalWorkItem>();
        }

        QueueBatchWork(batch with { TileWork = tileWork, AnimalWork = animalWork }, location);
        StartNextAnimalOrTileOrAdvance();
    }

    /// <summary>The worker is back on the farm; the batch index was advanced before the travel started.</summary>
    private void CompleteWorkExitTravel()
    {
        if (_ctx is null)
            return;

        _currentLocation = Game1.getFarm();
        BeginCurrentBatch();
    }

    /// <summary>The worker is in the deposit trip's chest location; walk to the chest and deposit.</summary>
    private void CompleteDepositEntryTravel()
    {
        if (_currentTrip is not { Destination: ChestDestination chestDest })
        {
            FinalizeAndAdvanceTrip(Game1.getFarm());
            return;
        }

        _ctx!.StateMachine.SetIntent(ToDepositIntent(_currentTrip));
        StartChestDepositNavigation(_currentTrip, chestDest, _currentLocation ?? Game1.getFarm());
    }

    private void SkipCurrentBatchAfterEntryFailure()
    {
        if (_ctx is null)
            return;

        _buildingNavigator.LogSkipped(_ctx.Batches[_ctx.CurrentBatchIndex].LocationName);
        _ctx.CurrentBatchIndex++;
        BeginCurrentBatch();
    }

    private static string DescribeTravelDestination(TravelPlan plan)
    {
        if (plan.Legs.Count == 0)
            return "none";

        var final = plan.Legs[^1];
        var location = final.WarpTarget ?? final.WalkLocation;
        var tile = final.WarpTarget is not null ? final.WarpArrivalTile : final.WalkTarget;
        return $"{location.NameOrUniqueName}@({tile.X},{tile.Y})";
    }
}
