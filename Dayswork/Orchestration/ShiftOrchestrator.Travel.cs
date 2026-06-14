using Dayswork.Compat;
using Dayswork.Core.Compat;
using Dayswork.Core.Domain;
using Dayswork.Core.Shifts;
using Dayswork.Integration;
using Dayswork.Worker;
using StardewModdingAPI;
using StardewValley;

namespace Dayswork.Orchestration;

/// <summary>What the orchestrator does when the active travel completes (or fails).</summary>
internal enum TravelPurpose
{
    None,
    WorkEntry,       // into the active batch's work location
    WorkExit,        // back to the farm after a batch (batch index already advanced)
    DepositEntry,    // to the active deposit trip's chest location
    DepositExit,     // back to the farm after an interior deposit
    ShoppingStep,    // managed shopping; sub-dispatched on the shopping phase
    ManagedReentry,  // back into the managed-crop field location after shopping
    ExitForDeposit,  // leave a building interior before beginning the deposit run
}

internal sealed partial class ShiftOrchestrator
{
    internal void StartTravel(TravelPlan plan, TravelPurpose purpose)
    {
        if (Session.Worker is null)
            return;

        _toolAnimator.StopSwing();
        Session.TravelPurpose = purpose;

        if (plan.Legs.Count > 0)
        {
            var firstLeg = plan.Legs[0];
            Session.PendingNavTile = firstLeg.WalkTarget;
            Session.PendingTaskTile = firstLeg.WalkTarget;
            EnsureWorkingIntent(new IntentMoveToTile(firstLeg.WalkTarget));
            Session.CurrentLocation = firstLeg.WalkLocation;
        }

        DevLog.Log(
            $"[Dayswork][travel] start purpose={purpose} legs={plan.Legs.Count} policy={plan.OnFailure} " +
            $"destination={DescribeTravelDestination(plan)}.");
        _travel.Start(plan, Session.Worker);
    }

    private void HandleTravel()
    {
        _travel.Update();
        if (Session.Worker?.currentLocation is { } location)
            Session.CurrentLocation = location;

        if (_travel.Failed)
        {
            var purpose = Session.TravelPurpose;
            var detail = _travel.FailureDetail;
            CancelActiveTravel();
            OnTravelFailed(purpose, detail);
            return;
        }

        if (!_travel.IsComplete)
            return;

        var arrived = Session.TravelPurpose;
        if (_travel.CompletedWithForcedWarp)
            DevLog.Log(
                $"[Dayswork][travel] purpose={arrived} forced warp to destination ({_travel.FailureDetail}).",
                DevLog.WarnLevel);
        CancelActiveTravel();
        OnTravelArrived(arrived);
    }

    internal void CancelActiveTravel()
    {
        _travel.Clear();
        if (_session is { } s)
            s.TravelPurpose = TravelPurpose.None;
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
                Session.Deposits.OnDepositEntryTravelArrived();
                break;
            case TravelPurpose.DepositExit:
                Session.Deposits.FinalizeAndAdvanceTrip(Game1.getFarm());
                break;
            case TravelPurpose.ShoppingStep:
                Session.Shopping.OnTravelArrived();
                break;
            case TravelPurpose.ExitForDeposit:
                BeginDeposit();
                break;
        }
    }

    private void OnTravelFailed(TravelPurpose purpose, string? detail)
    {
        DevLog.Log($"[Dayswork][travel] purpose={purpose} failed: {detail}.", DevLog.WarnLevel);
        switch (purpose)
        {
            case TravelPurpose.WorkEntry:
                SkipCurrentBatchAfterEntryFailure();
                break;
            case TravelPurpose.DepositEntry:
                Session.Deposits.OnDepositEntryTravelFailed(Game1.getFarm());
                break;
            case TravelPurpose.ShoppingStep:
                Session.Shopping.AbortTripForNavigationFailure();
                break;
        }
    }

    // ---- plan builders ----

    /// <summary>Farm → building interior: walk to the outdoor door, warp to the interior entry tile.</summary>
    internal bool TryBuildBuildingEntryPlan(
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
    internal TravelPlan BuildBuildingExitPlan(GameLocation interior, TileCoord farmArrivalTile)
    {
        var exitCandidates = _buildingNavigator.ResolveInteriorExitApproachTiles(interior);
        var source = new TileCoord(Session.Worker!.TilePoint.X, Session.Worker.TilePoint.Y);
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
    internal static TravelPlan WalkOnlyPlan(
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
    internal bool TryStartExpansionTravel(
        string sourceLocationName,
        string targetLocationName,
        ExpansionRoutePurpose routePurpose,
        TravelFailurePolicy policy,
        TravelPurpose travelPurpose)
    {
        if (Session.Worker is null || ModEntry.ExpansionCompat is not { } compat)
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
        if (_session is null || Session.Worker is null)
            return;

        var batch = Session.Ctx.Batches[Session.Ctx.CurrentBatchIndex];
        var location = Session.Worker.currentLocation ?? Game1.getLocationFromName(batch.LocationName);
        if (location is null)
        {
            Session.Ctx.CurrentBatchIndex++;
            BeginCurrentBatch();
            return;
        }

        Session.CurrentLocation = location;
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
                Session.Ctx.ToolSnapshot,
                OutputScopeProvenance.AnimalBuilding(batch.LocationName));
            if (batch.FeedBuilding)
            {
                Session.CurrentFeedPlan = _animalHandler.CreateFeedWork(location);
                tileWork = Session.CurrentFeedPlan.WorkItems.Concat(tileWork).ToList();
                Session.HayInHand = 0;
            }
            else
            {
                Session.CurrentFeedPlan = null;
                Session.HayInHand = 0;
            }

            var selectedHome = new HashSet<string>(StringComparer.Ordinal) { batch.LocationName };
            animalWork = BuildAnimalWork(location, selectedHome, batchTasks, Session.PriorityOrderer);
        }
        else
        {
            Session.CurrentFeedPlan = null;
            Session.HayInHand = 0;
            tileWork = _indoorScanner.ScanInterior(
                location,
                batchTasks,
                Session.Ctx.ToolSnapshot,
                OutputScopeProvenance.Greenhouse(batch.LocationName));
            animalWork = Array.Empty<AnimalWorkItem>();
        }

        QueueBatchWork(batch with { TileWork = tileWork, AnimalWork = animalWork }, location);
        StartNextAnimalOrTileOrAdvance();
    }

    /// <summary>The worker is back on the farm; the batch index was advanced before the travel started.</summary>
    private void CompleteWorkExitTravel()
    {
        if (_session is null)
            return;

        Session.CurrentLocation = Game1.getFarm();
        BeginCurrentBatch();
    }

    private void SkipCurrentBatchAfterEntryFailure()
    {
        if (_session is null)
            return;

        _buildingNavigator.LogSkipped(Session.Ctx.Batches[Session.Ctx.CurrentBatchIndex].LocationName);
        Session.Ctx.CurrentBatchIndex++;
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
