using Dayswork.Core.Domain;
using Dayswork.Core.Energy;
using Dayswork.Core.FishPonds;
using Dayswork.Core.Shifts;
using Dayswork.Worker;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Buildings;
using SObject = StardewValley.Object;

namespace Dayswork.Orchestration;

/// <summary>
/// Manage Fish Ponds execution. A fish-pond batch (one per location with selected ponds) visits each
/// ready pond and collects its finished produce into the deposit buffer. Collect-only — the player
/// stocks the fish and supplies any capacity-quest item, so there is none of the input/reload/fetch
/// machinery the machine batch has. Mirrors the machine batch lifecycle minus the reload passes.
///
/// Collection is a direct field write (capture <c>pond.output.Value</c>, null it, credit the buffer),
/// which avoids the player-inventory/HUD/xp entanglement of <c>FishPond.doAction</c> — so no fake
/// worker <c>Farmer</c> or action guard is needed (see <c>docs/machines.md</c> → "Fish ponds").
/// </summary>
internal sealed partial class ShiftOrchestrator
{
    private readonly FishPondReader _fishPondReader = new();

    private static bool IsFishPondBatch(WorkBatch batch) => batch.Kind == BatchKind.FishPonds;

    private void ResetFishPondState()
    {
        if (_session is not { } s)
            return;

        s.FishPondSteps.Clear();
        s.CurrentFishPondStep = null;
        s.FishPondsActive = false;
        s.FishPondBatchLocationName = "Farm";
    }

    private static GameLocation? ResolveFishPondBatchLocation(string locationName) =>
        string.Equals(locationName, "Farm", StringComparison.Ordinal)
            ? Game1.getFarm()
            : Game1.getLocationFromName(locationName);

    private void BeginFishPondBatch(WorkBatch batch)
    {
        if (_session is null || Session.Worker is null)
            return;

        ResetFishPondState();
        Session.FishPondBatchLocationName = batch.LocationName;

        var location = ResolveFishPondBatchLocation(batch.LocationName);
        var scope = Session.Ctx.WorkScopes.FishPonds;
        if (location is null || scope is null)
        {
            DevLog.Log($"[Dayswork][fishponds] skipped batch={batch.LocationName} reason=location_or_scope_unavailable.", DevLog.WarnLevel);
            Session.Ctx.CurrentBatchIndex++;
            BeginCurrentBatch();
            return;
        }

        Session.CurrentLocation = location;

        foreach (var pondRef in scope.Ponds.Where(pond =>
                     string.Equals(pond.LocationName, batch.LocationName, StringComparison.Ordinal)))
        {
            var live = _fishPondReader.Resolve(location, pondRef.Tile);
            if (live is null)
            {
                DevLog.Log($"[Dayswork][fishponds] skip pond ({pondRef.Tile.X},{pondRef.Tile.Y}) — no fish pond there anymore.", DevLog.WarnLevel);
                continue;
            }

            if (FishPondReader.HasOutput(live))
                Session.FishPondSteps.Enqueue(new FishPondStep(pondRef));
            else
                DevLog.Log($"[Dayswork][fishponds] skip ({pondRef.Tile.X},{pondRef.Tile.Y}) — no output ready.", LogLevel.Debug);
        }

        ModEntry.ModMonitor.Log(
            $"[Dayswork][fishponds] batch={batch.LocationName} readyPonds={Session.FishPondSteps.Count}.",
            DevLog.WarnLevel);

        if (Session.FishPondSteps.Count == 0)
        {
            Session.Ctx.CurrentBatchIndex++;
            BeginCurrentBatch();
            return;
        }

        Session.FishPondsActive = true;
        StartNextFishPondStep();
    }

    private void StartNextFishPondStep()
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

        var location = Session.CurrentLocation ?? ResolveFishPondBatchLocation(Session.FishPondBatchLocationName) ?? Game1.getFarm();

        while (Session.FishPondSteps.Count > 0)
        {
            var step = Session.FishPondSteps.Dequeue();
            var live = _fishPondReader.Resolve(location, step.Pond.Tile);
            if (live is null || !FishPondReader.HasOutput(live))
                continue;

            Session.CurrentFishPondStep = step;
            var navTile = ResolveFishPondNavTile(live, location);
            Session.PendingNavTile = navTile;
            Session.PendingTaskTile = step.Pond.Tile;
            EnsureWorkingIntent(new IntentMoveToTile(navTile));
            _nav.StartNavigation(navTile, location, Session.Worker);
            return;
        }

        CompleteFishPondBatch();
    }

    private void HandleFishPondAction(IntentPerformFishPondAction intent, GameLocation location)
    {
        if (_session is null || Session.Worker is null)
            return;

        var step = Session.CurrentFishPondStep;
        if (step is null)
        {
            StartNextFishPondStep();
            return;
        }

        if (!Session.ActionPending)
        {
            _toolAnimator.StopSwing();
            _toolAnimator.PlaySwing(WorkerTool.None, FacingToward(Session.Worker.TilePoint, step.Pond.Tile, Session.Worker.FacingDirection));

            var live = _fishPondReader.Resolve(location, step.Pond.Tile);
            if (live is not null)
            {
                CollectFishPond(live, step);
                SpendStaminaForBeat(WorkActionKind.CollectFishPond);
            }

            Session.ActionPending = true;
            return;
        }

        if (_toolAnimator.IsSwinging)
            return;

        Session.ActionPending = false;
        Session.CurrentFishPondStep = null;

        var boundary = _boundaryClassifier.EvaluateAfterBeat(
            unitResolved: true,
            Session.Ctx.EnergyState,
            HasBoundaryStopRequested());

        if (boundary.ShouldWrapUpAfterCurrentUnit)
        {
            QueueWrapUpNow(Session.Ctx.PendingStopReason ?? ShiftStopReason.Exhausted);
            return;
        }

        RecordActiveBatchProgress();
        StartNextFishPondStep();
    }

    private void CollectFishPond(FishPond pond, FishPondStep step)
    {
        if (_session is null)
            return;

        var output = pond.output.Value;
        if (output is null)
            return;

        var qualifiedId = output.QualifiedItemId;
        var stack = Math.Max(1, output.Stack);
        var quality = output is SObject obj ? obj.Quality : 0;
        if (string.IsNullOrWhiteSpace(qualifiedId))
            return;

        // Capture flavored/colored identity (e.g. "Sturgeon Roe" vs plain Roe) before nulling the
        // field, so the deposit pipeline rebuilds the exact item — flavor, color, and sell price —
        // rather than a generic one keyed only by id.
        var flavorId = Session.Flavors.Register(output);

        // Direct field manipulation: clear the held output, then credit the buffer (duplication-safe
        // — only credit after the pond actually released its output). No player-inventory/HUD/xp side
        // effects, so no action guard is needed.
        pond.output.Value = null;

        Session.Ctx.Buffer.Add(qualifiedId, stack, TaskKind.HarvestCrops, FishPondOutputRouter.Provenance, quality, flavorId);
        DevLog.Log($"[Dayswork][fishponds] collected {stack}x {qualifiedId} (q{quality}, flavor={flavorId ?? "none"}) at ({step.Pond.Tile.X},{step.Pond.Tile.Y}).", LogLevel.Debug);
    }

    private TileCoord ResolveFishPondNavTile(FishPond pond, GameLocation location)
    {
        if (Session.Worker is not null)
        {
            var source = new TileCoord(Session.Worker.TilePoint.X, Session.Worker.TilePoint.Y);
            var routeCosts = WorkerMovementDriver.ComputeRouteCostsFrom(source, location);
            if (WorkerRouteSelector.TrySelectNearestReachableTile(
                    FishPondReader.StandTilesAround(pond), routeCosts, out var stand))
                return stand;
        }

        // Fallback: directly below the footprint (where the output bucket renders).
        return new TileCoord(pond.tileX.Value, pond.tileY.Value + Math.Max(1, pond.tilesHigh.Value));
    }

    private void CompleteFishPondBatch()
    {
        if (_session is null)
            return;

        var completedLocation = Session.FishPondBatchLocationName;
        ResetFishPondState();

        if (TryStartFishPondBatchExitTravel(completedLocation))
            return;

        Session.Ctx.CurrentBatchIndex++;
        BeginCurrentBatch();
    }

    private bool TryStartFishPondBatchExitTravel(string locationName)
    {
        if (_session is null || Session.Worker is null)
            return false;

        var farm = Game1.getFarm();
        if (string.Equals(locationName, "Farm", StringComparison.Ordinal))
        {
            Session.CurrentLocation = farm;
            return false;
        }

        var current = Session.Worker.currentLocation ?? Session.CurrentLocation;
        if (current is null || current == farm)
        {
            Session.CurrentLocation = farm;
            return false;
        }

        if (ModEntry.ExpansionCompat is { } compat &&
            compat.TryGetExpansionLocationDescriptor(locationName, out _))
        {
            Session.Ctx.CurrentBatchIndex++;
            if (!TryStartExpansionTravel(
                    current.NameOrUniqueName,
                    "Farm",
                    Dayswork.Core.Compat.ExpansionRoutePurpose.ReturnToFarm,
                    TravelFailurePolicy.WarpToDestination,
                    TravelPurpose.WorkExit))
            {
                WarpExpansionWorkerToFarm();
                BeginCurrentBatch();
            }

            return true;
        }

        var farmArrival = _buildingNavigator.TryResolveDoorTile(locationName, out var outdoorDoor, out _)
            ? outdoorDoor
            : Session.FarmExitTile;
        Session.Ctx.CurrentBatchIndex++;
        StartTravel(BuildBuildingExitPlan(current, farmArrival), TravelPurpose.WorkExit);
        return true;
    }
}

/// <summary>One fish-pond visit: collect the ready output into the deposit buffer (collect-only).</summary>
internal sealed record FishPondStep(FishPondRef Pond);
