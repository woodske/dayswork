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

internal sealed class ShiftOrchestrator : ISessionBoundaryResettable
{
    private enum PendingExpansionRouteKind
    {
        None,
        WorkEntry,
        WorkExit,
        DepositEntry,
        DepositExit,
    }

    // Shipping bin tile on Standard Farm.
    private static readonly TileCoord ShippingBinTile = new(71, 13);

    // Emote IDs — play-test TODO: confirm "?" and "!" are 8 and 2 in vanilla.
    // See code-summary.md play-test checklist.
    private const int EmoteQuestion    = 8;  // confused "?" (stuck step 1)
    private const int EmoteExclamation = 2;  // surprised "!" (hit reaction)

    // Melee proximity range for hit-detection (Manhattan distance in tiles).
    private const float HitRangeTiles = 2.0f;

    // Brief morning hold so the player sees the worker enter from the farm entrance.
    // Vanilla tree debris can spawn after the tree-fall animation, not on the axe-hit tick.
    private const int ImmediateDebrisSweepRadiusTiles = 3;
    private const int DelayedTreeDebrisSweepTicks = 240;
    private const int DelayedTreeDebrisSweepRadiusTiles = 6;

    private readonly ToolLevelReader      _toolReader;
    private readonly ToolSwapAnimator     _toolAnimator;
    // Rebuilt per shift from the active contract's player-defined category priority (see StartShift).
    private ITaskPriorityOrderer _priorityOrderer = new TaskPriorityOrderer();
    private readonly ShiftPlanBuilder     _shiftPlanBuilder = new();
    private readonly IWorkScopeClassifier _scopeClassifier;
    private IConfigSnapshot               _config;
    private readonly WorkerMovementDriver _nav;
    private readonly WorkAreaScanner      _workAreaScanner;
    private readonly IndoorWorkScanner    _indoorScanner;
    private readonly AnimalTaskHandler    _animalHandler;
    private readonly BuildingWorkNavigator _buildingNavigator;
    private readonly ChestResolver        _chestResolver;
    private readonly IDepositPlanner      _depositPlanner;
    private readonly IMailDispatcher      _mailDispatcher;
    private readonly WorkerEnergyLedger _energyLedger = new();
    private readonly WorkUnitBoundaryClassifier _boundaryClassifier = new();
    private readonly OverflowCategorizer _overflowCategorizer = new();
    private readonly WorkerRouteSelector _routeSelector = new();
    private readonly CrossLocationRouteNavigator _expansionRouteNavigator;

    private ShiftContext? _ctx;
    private FarmhandNpc?  _farmhand;
    private GameLocation? _currentLocation;
    private int           _tickCount;
    private int           _morningEntranceHoldTicks;
    // Farm exit warp tile — computed once per shift from farm.warps (not a static constant,
    // because the warp tile varies by farm type and player map edits).
    private TileCoord     _farmExitTile;

    // Multi-trip deposit loop state (Pattern N): the ordered remaining trips and the in-flight one.
    private readonly Queue<DepositTrip> _depositTrips = new();
    private DepositTrip? _currentTrip;

    // Guards the FarmForage pre-completion rescan against re-enqueuing the same tile forever.
    // A forage tile the worker cannot reach (or cannot remove) would otherwise be re-detected on
    // every completion cycle once ClearRemainingActiveBatchWork drops it from the work queues,
    // producing an infinite "rescan picked up 1 new tile item" loop. We enqueue each tile at most
    // once per batch; the set resets when the active batch index changes.
    private int _rescanBatchIndex = -1;
    private readonly HashSet<TileCoord> _rescanEnqueuedTiles = new();

    // Per-stack paced execution within the in-flight trip. Each stack is one beat gated by
    // _toolAnimator.IsSwinging (duration == WorkerActionAnimationMs), so the deposit cadence
    // tracks the same config knob as task actions.
    private int           _currentTripStackIndex;
    private bool          _currentTripExecutionStarted;
    private Chest?        _currentTripChest;
    private GameLocation? _currentTripLocation;
    private bool          _currentTripChestAnimated;   // we triggered the open-lid animation
    // Set while the worker is walking across the farm to a building door for an interior-chest
    // deposit. Cleared when we cross the door (warp into the interior) on arrival.
    private GameLocation? _pendingDepositInterior;
    // Set while the worker is walking to the interior exit door after depositing in a building.
    // Cleared when we cross the door (warp back to the farm) on arrival.
    private bool          _pendingDepositExit;
    private PendingExpansionRouteKind _pendingExpansionRouteKind;
    private WorkBatch? _pendingExpansionRouteBatch;

    // Per-WorkItem state — the nav tile and task tile are tracked separately (trellis crops).
    private bool      _actionPending;
    private TaskKind  _pendingTask;
    private TileCoord _pendingNavTile;
    private TileCoord _pendingTaskTile;
    private OutputScopeProvenance _pendingOutputProvenance = OutputScopeProvenance.Unknown;
    private bool      _waitingForDebrisBeforeDeposit;
    private bool      _pendingBuildingEntry;
    private bool      _pendingBuildingExit;
    private TileCoord _pendingBuildingOutdoorDoor;
    private GameLocation? _pendingBuildingInterior;
    private TileCoord _pendingInteriorExitTile;
    private FeedWorkPlan? _currentFeedPlan;
    private int _hayInHand;
    private LaborBeatOutcome? _pendingBeatOutcome;

    private readonly Queue<AnimalWorkItem> _animalWork = new();
    private readonly List<WorkItem> _deferredTileWork = new();
    private readonly List<AnimalWorkItem> _deferredAnimalWork = new();
    private int _activeBatchSelectionAttempts;
    private int _activeBatchMaxSelectionAttempts = 4;
    private WorkItem? _currentTileWork;
    private AnimalWorkItem? _currentAnimalWork;

    // Stuck detection. Replaced after first teleport recovery to switch threshold.
    private IStuckDetector _stuck;

    // Time tracking for stuck accumulation (game uses HHMM format; 10-unit increments).
    private int _lastSampledGameTime;

    // Last observed tile position for progress detection (Pattern D / FD-Q3=A).
    private Point _lastTilePos;

    // Hit-reaction debounce — one emote per player swing.
    private bool _playerWasSwinging;

    private readonly List<PendingDebrisSweep> _pendingDebrisSweeps = new();

    public ShiftOrchestrator(
        ToolLevelReader toolReader,
        IConfigSnapshot config,
        IWorkScopeClassifier scopeClassifier,
        ToolSwapAnimator toolAnimator,
        WorkerMovementDriver nav,
        WorkAreaScanner workAreaScanner,
        IndoorWorkScanner indoorScanner,
        AnimalTaskHandler animalHandler,
        BuildingWorkNavigator buildingNavigator,
        ChestResolver chestResolver,
        IDepositPlanner depositPlanner,
        IMailDispatcher mailDispatcher)
    {
        _toolReader        = toolReader;
        _config            = config;
        _scopeClassifier   = scopeClassifier;
        _toolAnimator      = toolAnimator;
        _nav               = nav;
        _workAreaScanner   = workAreaScanner;
        _indoorScanner     = indoorScanner;
        _animalHandler     = animalHandler;
        _buildingNavigator = buildingNavigator;
        _chestResolver     = chestResolver;
        _depositPlanner    = depositPlanner;
        _mailDispatcher    = mailDispatcher;
        _expansionRouteNavigator = new CrossLocationRouteNavigator(nav);
        _stuck             = new StuckDetector(config.StuckInitialWaitMinutes);
    }

    private static int Manhattan(TileCoord a, TileCoord b) =>
        Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);

    /// <summary>
    /// Locates the best navigable approach tile for the farm's external exit by scanning
    /// <see cref="Farm.warps"/>. Warps targeting building interiors are excluded; the first
    /// remaining warp leads to the outside world (BusStop, Town, etc.).
    /// SDV warp triggers are sometimes stored one tile outside the map boundary (the player
    /// "walks off the edge" to fire them). The tile is clamped to the map boundary and we
    /// then search inward along the exit corridor for the nearest passable tile.
    /// </summary>
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

    /// <summary>
    /// Returns <paramref name="preferred"/> when a worker can stand on it; otherwise searches outward
    /// in expanding rings for the nearest passable tile, clamped to the map. Used for expansion
    /// entrance overrides, where the configured spawn tile is a preference that may be blocked at
    /// spawn time (placed objects, seasonal debris). Falls back to the preferred tile if nothing
    /// passable is found within the search radius (HandleExit logs if navigation then fails).
    /// </summary>
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
                    LogLevel.Debug);
                return new TileCoord(x, y);
            }
        }

        ModEntry.ModMonitor.Log(
            $"[Dayswork] Entrance override ({preferred.X},{preferred.Y}) blocked and no passable tile found within {maxRadius} tiles — using it anyway.",
            LogLevel.Warn);
        return preferred;
    }

    private bool HasBoundaryStopRequested() => _ctx?.PendingStopReason is not null;

    private bool IsWorkUnitInProgress() => _actionPending;

    private bool ShouldWrapUpBeforeNextUnit() =>
        _ctx is not null && (_ctx.PendingStopReason is not null || !_ctx.EnergyState.CanStartNewWorkUnit);

    private void RequestBoundaryStop(ShiftStopReason reason, int? stopTime = null)
    {
        if (_ctx is null)
            return;

        _ctx.PendingStopReason ??= reason;
        _ctx.ShiftEndTime ??= stopTime ?? Game1.timeOfDay;
    }

    private void QueueWrapUpNow(ShiftStopReason reason, int? stopTime = null)
    {
        if (_ctx is null)
            return;

        RequestBoundaryStop(reason, stopTime);
        BeginDeposit();
    }

    private void SpendStaminaForBeat(WorkActionKind action)
    {
        if (_ctx is null)
            return;

        var spendResult = _energyLedger.ApplyActionCost(_ctx.EnergyState, action);
        _ctx.EnergyState = spendResult.State;
        _farmhand?.SetStamina(_ctx.EnergyState.RemainingEnergy, _ctx.EnergyState.Capacity);
        if (spendResult.ReachedZeroOnThisBeat)
            RequestBoundaryStop(ShiftStopReason.Exhausted);
    }

    private void FinishResolvedAnimalWork(GameLocation location, bool madeProgress = true)
    {
        if (_ctx is null)
            return;

        var boundary = _boundaryClassifier.EvaluateAfterBeat(
            unitResolved: true,
            energyState: _ctx.EnergyState,
            boundaryStopRequested: HasBoundaryStopRequested());

        if (boundary.ShouldWrapUpAfterCurrentUnit)
        {
            QueueWrapUpNow(_ctx.PendingStopReason ?? ShiftStopReason.Exhausted);
            return;
        }

        if (madeProgress)
            RecordActiveBatchProgress();

        StartNextAnimalOrTileOrAdvance();
    }

    private static WorkActionKind ActionKindForTask(TaskKind task) =>
        task switch
        {
            TaskKind.WaterCrops => WorkActionKind.WaterTile,
            TaskKind.HarvestCrops => WorkActionKind.HarvestCrop,
            TaskKind.CollectFruit => WorkActionKind.HarvestFruit,
            TaskKind.FeedAnimals => WorkActionKind.FeedAnimal,
            TaskKind.PetAnimals => WorkActionKind.PetAnimal,
            TaskKind.CollectAnimalProducts => WorkActionKind.CollectAnimalProduct,
            TaskKind.CutTrees => WorkActionKind.AxeSwing,
            TaskKind.ClearRocks => WorkActionKind.PickaxeSwing,
            TaskKind.ClearWeeds or TaskKind.ClearGrass => WorkActionKind.ScytheSwing,
            _ => throw new ArgumentOutOfRangeException(nameof(task), task, null),
        };

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>Non-null while a shift is running; null between shifts.</summary>
    public ContractId? ActiveContractId => _ctx?.ContractId;

    /// <summary>
    /// Immediately ends the current shift: worker deposits buffered items and exits.
    /// Safe to call from any active working phase (Working, Stuck, Recovering).
    /// No-op if no shift is active or the shift is already wrapping up.
    /// </summary>
    public void EndShiftEarly()
    {
        if (_ctx is null)
        {
            ModEntry.ModMonitor.Log("[Dayswork] No active shift to cancel.", LogLevel.Info);
            return;
        }

        var phase = _ctx.StateMachine.Phase;
        if (phase is ShiftPhase.Depositing or ShiftPhase.Exiting or ShiftPhase.Done)
        {
            ModEntry.ModMonitor.Log("[Dayswork] Shift is already finishing — nothing to cancel.", LogLevel.Info);
            return;
        }

        ModEntry.ModMonitor.Log("[Dayswork] Player cancelled shift early — worker will deposit and leave.", LogLevel.Info);
        _ctx.ShiftEndTime = Game1.timeOfDay;

        // Stuck is a transient working state; bridge it to Recovering so that
        // BeginDeposit (which transitions Recovering → Depositing) is legal.
        if (phase == ShiftPhase.Stuck)
            _ctx.StateMachine.Transition(ShiftPhase.Recovering, new IntentTeleportHome());

        QueueWrapUpNow(ShiftStopReason.Cancelled);
    }

    public void ResetForSessionBoundary(SessionResetBoundary boundary)
    {
        var hadRuntimeState = _ctx is not null ||
                              _farmhand is not null ||
                              _currentLocation is not null ||
                              _depositTrips.Count > 0 ||
                              _pendingDebrisSweeps.Count > 0 ||
                              _animalWork.Count > 0 ||
                              _deferredTileWork.Count > 0 ||
                              _deferredAnimalWork.Count > 0 ||
                              _currentTrip is not null ||
                              _currentTileWork is not null ||
                              _currentAnimalWork is not null;

        if (hadRuntimeState)
        {
            ModEntry.ModMonitor.Log(
                $"[Dayswork] Resetting in-memory worker runtime for session boundary {boundary}.",
                LogLevel.Info);
        }

        ClearWorker();
        _ctx = null;
        _tickCount = 0;
        _farmExitTile = default;
        _lastSampledGameTime = 0;
        _lastTilePos = default;
        _playerWasSwinging = false;
        _stuck = new StuckDetector(_config.StuckInitialWaitMinutes);
    }

    public void StartShift(Contract contract, IConfigSnapshot runtimeConfig)
    {
        if (_ctx is not null)
        {
            ModEntry.ModMonitor.Log("[Dayswork] StartShift called while a shift is already active — ignoring.", LogLevel.Warn);
            return;
        }

        _config = runtimeConfig;
        _priorityOrderer = new TaskPriorityOrderer(contract.CategoryPriority);
        var contractTerms = contract.TermsSnapshot;
        var energyState = _energyLedger.StartShift(contractTerms.Energy);
        var pacingProfile = WorkerPacingProfile.FromConfig(runtimeConfig);

        var farm     = Game1.getFarm();
        var snapshot = _toolReader.ReadSnapshot(Game1.player);
        var runtimeScopeSelection = NormalizeRuntimeScopeSelection(contract.ScopeSelection, farm);
        var workScopes = _scopeClassifier.Classify(runtimeScopeSelection, contract.EnabledTasks);

        _farmExitTile = FindFarmExitTile(farm);
        var batches = BuildInitialBatches(contract, workScopes, farm, snapshot);

        if (batches.Count == 0 ||
            batches.All(batch => batch.Kind is BatchKind.OutdoorAnimals or BatchKind.OutdoorCrops or BatchKind.OutdoorClearing or BatchKind.FarmForage &&
                                 batch.TileWork.Count == 0 &&
                                 batch.AnimalWork.Count == 0 &&
                                 !batch.FeedBuilding))
        {
            ModEntry.ModMonitor.Log("[Dayswork] No applicable work found for today's contract — no worker spawned.", LogLevel.Info);
            return;
        }

        var spawnPos = new Vector2(_farmExitTile.X, _farmExitTile.Y) * 64f;
        _farmhand = new FarmhandNpc(spawnPos);
        farm.addCharacter(_farmhand);
        _currentLocation = farm;
        _toolAnimator.SetWorker(_farmhand);
        _toolAnimator.SetPacingProfile(pacingProfile);
        _nav.SetPacingProfile(pacingProfile);
        _farmhand.SetStamina(energyState.RemainingEnergy, energyState.Capacity);

        // Reset shift-level state.
        _stuck              = new StuckDetector(_config.StuckInitialWaitMinutes);
        _lastSampledGameTime = Game1.timeOfDay;
        _lastTilePos         = _farmhand.TilePoint;
        _playerWasSwinging   = false;
        _actionPending       = false;
        _waitingForDebrisBeforeDeposit = false;
        _pendingBuildingEntry = false;
        _pendingBuildingExit = false;
        _pendingBuildingInterior = null;
        _currentFeedPlan = null;
        _hayInHand = 0;
        _animalWork.Clear();
        _deferredTileWork.Clear();
        _deferredAnimalWork.Clear();
        _currentTileWork = null;
        _currentAnimalWork = null;
        _activeBatchSelectionAttempts = 0;
        _activeBatchMaxSelectionAttempts = 4;
        _pendingDebrisSweeps.Clear();
        _pendingBeatOutcome = null;
        _pendingOutputProvenance = OutputScopeProvenance.Unknown;
        _pendingExpansionRouteKind = PendingExpansionRouteKind.None;
        _pendingExpansionRouteBatch = null;
        _expansionRouteNavigator.Clear();

        _ctx = new ShiftContext(
            contractId:       contract.Id,
            workScopes:       workScopes,
            enabledTasks:     contract.EnabledTasks,
            taskDestinations: contract.TaskDestinations,
            contractTerms:    contractTerms,
            energyState:      energyState,
            pacingProfile:    pacingProfile,
            toolSnapshot:     snapshot,
            workList:         Array.Empty<WorkItem>(),
            shiftStartTime:   Game1.timeOfDay,
            batches:          batches);

        _morningEntranceHoldTicks = pacingProfile.EntranceHoldTicks;
        BeginCurrentBatch();
    }

    private static ContractScopeSelection NormalizeRuntimeScopeSelection(
        ContractScopeSelection selection,
        Farm farm)
    {
        var outdoorZones = selection.OutdoorZones
            .Select(zone => zone with { LocationName = "Farm" })
            .ToList();

        var animalBuildings = selection.AnimalBuildings
            .Select(building => building with
            {
                LocationName = BuildingLocationResolver.NormalizeLocationName(farm, building.LocationName),
            })
            .Where(building => !string.IsNullOrWhiteSpace(building.LocationName))
            .Distinct()
            .OrderBy(building => building.LocationName, StringComparer.Ordinal)
            .ThenBy(building => building.Tier)
            .ToList();

        var greenhouses = selection.Greenhouses
            .Select(greenhouse => NormalizeGreenhouseLocationName(farm, greenhouse.LocationName))
            .Where(locationName => !string.IsNullOrWhiteSpace(locationName))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(locationName => locationName, StringComparer.Ordinal)
            .Select(locationName => new GreenhouseSelection(locationName))
            .ToList();

        return new ContractScopeSelection(outdoorZones, animalBuildings, greenhouses);
    }

    // Expansion greenhouse work locations (e.g. SVE's Custom_GrandpasShedGreenhouse) are standalone
    // game locations, not farm buildings. The vanilla building resolver's loose substring fallback
    // would collapse "Custom_GrandpasShedGreenhouse" onto the vanilla "Greenhouse" building (the
    // request string contains "Greenhouse"), rewriting the batch location and sending the worker to
    // the wrong greenhouse so the expansion route never fires. Leave expansion location names
    // untouched; only vanilla greenhouse selections go through the building resolver.
    private static string NormalizeGreenhouseLocationName(Farm farm, string requestedName)
    {
        if (ModEntry.ExpansionCompat is { } compat &&
            compat.TryGetExpansionLocationDescriptor(requestedName, out _))
            return requestedName;

        return BuildingLocationResolver.NormalizeLocationName(farm, requestedName);
    }

    private IReadOnlyList<WorkBatch> BuildInitialBatches(
        Contract contract,
        WorkScopeSet workScopes,
        Farm farm,
        ToolSnapshot snapshot)
    {
        var skeletons = _shiftPlanBuilder.BuildBatchPlan(workScopes, contract.EnabledTasks);
        var outdoorZones = workScopes.OutdoorWork?.NormalizedZones ?? Array.Empty<Zone>();
        var outdoorProvenance = OutputScopeProvenance.Outdoor();
        var greenhouseLocation = workScopes.GreenhouseWork?.LocationName ?? "Greenhouse";

        var batches = new List<WorkBatch>(skeletons.Count);
        foreach (var batch in skeletons)
        {
            switch (batch.Kind)
            {
                case BatchKind.AnimalBuilding:
                case BatchKind.Greenhouse:
                    batches.Add(batch);
                    break;

                case BatchKind.OutdoorAnimals:
                {
                    // Per-building grazing pass (TODO-09): service only the grazing animals whose
                    // home key matches this batch's building. Farm-wide forage is handled separately
                    // by the trailing FarmForage batch.
                    var batchTasks = batch.Tasks.ToHashSet();
                    var buildingHomes = new HashSet<string>(StringComparer.Ordinal) { batch.LocationName };
                    var animalWork = BuildAnimalWork(farm, buildingHomes, batchTasks);
                    batches.Add(batch with { TileWork = Array.Empty<WorkItem>(), AnimalWork = animalWork });
                    break;
                }

                case BatchKind.FarmForage:
                {
                    // Single farm-wide ground-forage (truffle) sweep after all building visits.
                    var tileWork = _workAreaScanner.ScanWholeLocation(
                        farm,
                        batch.Tasks.ToHashSet(),
                        snapshot,
                        _farmExitTile,
                        OutputScopeProvenance.AnimalBuilding(string.Empty));
                    batches.Add(batch with { TileWork = tileWork, AnimalWork = Array.Empty<AnimalWorkItem>() });
                    break;
                }

                case BatchKind.OutdoorCrops:
                case BatchKind.OutdoorClearing:
                {
                    var batchTasks = batch.Tasks.ToHashSet();
                    var tileWork = outdoorZones.Count == 0
                        ? Array.Empty<WorkItem>()
                        : _workAreaScanner.ScanZones(
                            farm,
                            outdoorZones,
                            batchTasks,
                            snapshot,
                            _farmExitTile,
                            outdoorProvenance);
                    batches.Add(batch with { TileWork = tileWork });
                    break;
                }

                default:
                    throw new ArgumentOutOfRangeException(nameof(batch.Kind), batch.Kind, null);
            }
        }

        ModEntry.ModMonitor.Log(
            $"[Dayswork][shift-plan] runtime batches={string.Join(", ", batches.Select(batch => $"{batch.Kind}:{batch.LocationName}:{string.Join("/", batch.Tasks)}"))} greenhouse={greenhouseLocation} outdoorTiles={outdoorZones.Count}.",
            LogLevel.Info);
        return batches;
    }

    private IReadOnlyList<AnimalWorkItem> BuildAnimalWork(
        GameLocation location,
        IReadOnlySet<string> selectedAnimalHomes,
        IReadOnlySet<TaskKind> enabledTasks)
    {
        if (selectedAnimalHomes.Count == 0)
            return Array.Empty<AnimalWorkItem>();

        var work = new List<AnimalWorkItem>();
        foreach (var (animalRef, liveAnimal) in _animalHandler.EnumerateAnimals(location, selectedAnimalHomes))
        {
            var provenance = string.IsNullOrWhiteSpace(animalRef.HomeLocation)
                ? OutputScopeProvenance.AnimalBuilding(string.Empty)
                : OutputScopeProvenance.AnimalBuilding(animalRef.HomeLocation);

            if (enabledTasks.Contains(TaskKind.PetAnimals) && _animalHandler.ShouldPet(liveAnimal))
                work.Add(new AnimalWorkItem(location.Name, animalRef, TaskKind.PetAnimals, provenance));

            if (enabledTasks.Contains(TaskKind.CollectAnimalProducts) && _animalHandler.HasToolHarvestReady(liveAnimal))
                work.Add(new AnimalWorkItem(location.Name, animalRef, TaskKind.CollectAnimalProducts, provenance));
        }

        return work
            .GroupBy(item => item.Animal.Id)
            .SelectMany(group => group.OrderBy(item => _priorityOrderer.Order(new[] { item.Task })[0]))
            .ToList();
    }

    private bool IsAnimalHouseLocation(string locationName) =>
        _buildingNavigator.TryResolveInterior(locationName, out var interior) && interior is AnimalHouse;

    private void BeginCurrentBatch()
    {
        if (_ctx is null || _farmhand is null)
            return;

        _animalWork.Clear();
        _deferredTileWork.Clear();
        _deferredAnimalWork.Clear();
        _currentTileWork = null;
        _currentAnimalWork = null;
        _activeBatchSelectionAttempts = 0;
        _activeBatchMaxSelectionAttempts = 4;
        _pendingBuildingEntry = false;
        _pendingBuildingExit = false;
        _pendingBuildingInterior = null;
        _currentFeedPlan = null;
        _hayInHand = 0;

        if (_ctx.CurrentBatchIndex >= _ctx.Batches.Count)
        {
            QueueWrapUpNow(ShiftStopReason.Completed);
            return;
        }

        var batch = _ctx.Batches[_ctx.CurrentBatchIndex];
        if (IsExpansionGreenhouseBatch(batch))
        {
            if (TryStartExpansionRoute(
                    "Farm",
                    batch.LocationName,
                    ExpansionRoutePurpose.WorkEntry,
                    PendingExpansionRouteKind.WorkEntry,
                    batch))
                return;

            _ctx.CurrentBatchIndex++;
            BeginCurrentBatch();
            return;
        }

        if (BatchRequiresInteriorEntry(batch))
        {
            if (!_buildingNavigator.TryResolveDoorTile(batch.LocationName, out var outdoorDoor, out var interior))
            {
                _ctx.CurrentBatchIndex++;
                BeginCurrentBatch();
                return;
            }

            _pendingBuildingEntry = true;
            _pendingBuildingOutdoorDoor = outdoorDoor;
            _pendingBuildingInterior = interior;
            _pendingTask = TaskKind.FeedAnimals;
            _pendingNavTile = outdoorDoor;
            _pendingTaskTile = outdoorDoor;
            EnsureWorkingIntent(new IntentMoveToTile(outdoorDoor));
            _nav.StartNavigation(outdoorDoor, Game1.getFarm(), _farmhand);
            return;
        }

        _currentLocation = Game1.getFarm();
        if (batch.Kind == BatchKind.OutdoorAnimals)
            batch = RefreshBuildingGrazingWork(batch, _currentLocation);
        else if (batch.Kind == BatchKind.FarmForage)
            batch = RefreshFarmForageWork(batch, _currentLocation);
        QueueBatchWork(batch, _currentLocation);
        StartNextAnimalOrTileOrAdvance();
    }

    private WorkBatch RefreshBuildingGrazingWork(WorkBatch batch, GameLocation farm)
    {
        if (_ctx is null || batch.Kind != BatchKind.OutdoorAnimals)
            return batch;

        // Per-building grazing pass (TODO-09): rebuild this one building's grazing-animal work at
        // batch start (animals roam, so the shift-start snapshot is stale). Scoped to the single
        // building's home key; farm-wide forage is handled by the separate FarmForage batch.
        var buildingHomes = new HashSet<string>(StringComparer.Ordinal) { batch.LocationName };
        var batchTasks = batch.Tasks.ToHashSet();
        var refreshedAnimalWork = BuildAnimalWork(farm, buildingHomes, batchTasks);

        ModEntry.ModMonitor.Log(
            $"[Dayswork][building-grazing] home={batch.LocationName} animalWork={refreshedAnimalWork.Count}.",
            refreshedAnimalWork.Count == 0 ? LogLevel.Trace : LogLevel.Info);
        return batch with { TileWork = Array.Empty<WorkItem>(), AnimalWork = refreshedAnimalWork };
    }

    private WorkBatch RefreshFarmForageWork(WorkBatch batch, GameLocation farm)
    {
        if (_ctx is null || batch.Kind != BatchKind.FarmForage)
            return batch;

        // Truffles spawn on the farm continuously through the day as pigs forage. The initial
        // shift-start scan in BuildInitialBatches is hours stale by the time we actually start this
        // final farm-wide pass, so re-scan for forage-style animal products here.
        var batchTasks = batch.Tasks.ToHashSet();
        var refreshedTileWork = batchTasks.Contains(TaskKind.CollectAnimalProducts)
            ? _workAreaScanner.ScanWholeLocation(
                farm,
                batchTasks,
                _ctx.ToolSnapshot,
                _farmExitTile,
                OutputScopeProvenance.AnimalBuilding(string.Empty))
            : (IReadOnlyList<WorkItem>)Array.Empty<WorkItem>();

        ModEntry.ModMonitor.Log(
            $"[Dayswork][farm-forage] tileWork={refreshedTileWork.Count}.",
            refreshedTileWork.Count == 0 ? LogLevel.Trace : LogLevel.Info);
        return batch with { TileWork = refreshedTileWork, AnimalWork = Array.Empty<AnimalWorkItem>() };
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

        ModEntry.ModMonitor.Log(
            $"[Dayswork][farm-forage] pre-completion rescan picked up {added} new tile item(s); batch continues.",
            LogLevel.Info);
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

    private void CompleteCurrentBatch()
    {
        if (_ctx is null || _farmhand is null)
            return;

        var batch = _ctx.Batches[_ctx.CurrentBatchIndex];
        if (IsExpansionGreenhouseBatch(batch))
        {
            var currentName = (_currentLocation ?? _farmhand.currentLocation)?.NameOrUniqueName
                              ?? batch.LocationName;
            if (string.Equals(currentName, "Farm", StringComparison.OrdinalIgnoreCase))
            {
                CompleteExpansionWorkExit();
                return;
            }

            if (TryStartExpansionRoute(
                    currentName,
                    "Farm",
                    ExpansionRoutePurpose.ReturnToFarm,
                    PendingExpansionRouteKind.WorkExit,
                    batch))
                return;

            WarpExpansionWorkerToFarm();
            CompleteExpansionWorkExit();
            return;
        }

        if (BatchRequiresInteriorEntry(batch))
        {
            var interior = _currentLocation;
            if (interior is not null && interior != Game1.getFarm())
            {
                var exitCandidates = _buildingNavigator.ResolveInteriorExitApproachTiles(interior);
                var fallbackExitTile = exitCandidates[0];
                var source = new TileCoord(_farmhand.TilePoint.X, _farmhand.TilePoint.Y);
                var routeCosts = WorkerMovementDriver.ComputeRouteCostsFrom(source, interior);
                var exitTile = BuildingWorkNavigator.SelectNearestReachableExitApproachTile(
                    exitCandidates,
                    routeCosts,
                    fallbackExitTile);
                _pendingBuildingExit = true;
                _pendingInteriorExitTile = exitTile;
                _pendingTask = TaskKind.FeedAnimals;
                _pendingNavTile = exitTile;
                _pendingTaskTile = exitTile;
                _toolAnimator.StopSwing();
                EnsureWorkingIntent(new IntentMoveToTile(exitTile));
                _nav.StartNavigation(exitTile, interior, _farmhand);
                return;
            }
        }

        FinishCurrentBatchAfterBuildingExit();
    }

    private void FinishCurrentBatchAfterBuildingExit()
    {
        if (_ctx is null || _farmhand is null)
            return;

        var batch = _ctx.Batches[_ctx.CurrentBatchIndex];
        if (BatchRequiresInteriorEntry(batch))
        {
            _buildingNavigator.ExitToFarm(_farmhand, _pendingBuildingOutdoorDoor);
            _currentLocation = Game1.getFarm();
        }

        _pendingBuildingExit = false;
        _currentFeedPlan = null;
        _hayInHand = 0;
        _ctx.CurrentBatchIndex++;
        BeginCurrentBatch();
    }

    private void EnsureWorkingIntent(ShiftIntent intent)
    {
        if (_ctx is null)
            return;

        if (_ctx.StateMachine.Phase == ShiftPhase.WaitingForSpawn ||
            _ctx.StateMachine.Phase == ShiftPhase.Recovering)
        {
            _ctx.StateMachine.Transition(ShiftPhase.Working, intent);
            return;
        }

        _ctx.StateMachine.SetIntent(intent);
    }

    // ── SMAPI event handlers ─────────────────────────────────────────────────

    public void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
    {
        if (_ctx is null || _ctx.StateMachine.Phase == ShiftPhase.Done) return;
        if (!Game1.shouldTimePass(false)) return;
        if (_morningEntranceHoldTicks > 0 && _ctx.StateMachine.Phase == ShiftPhase.Working)
        {
            _morningEntranceHoldTicks--;
            return;
        }

        _toolAnimator.Update(Game1.currentGameTime);
        _nav.Update();
        ProcessPendingDebrisSweeps();
        if (_waitingForDebrisBeforeDeposit)
        {
            if (_pendingDebrisSweeps.Count == 0)
            {
                _waitingForDebrisBeforeDeposit = false;
                BeginDeposit();
            }
            return;
        }
        if (++_tickCount % 4 != 0) return; // PERF-U13-01 throttle

        var farm  = Game1.getFarm();
        var currentLocation = _currentLocation ?? farm;
        var phase = _ctx.StateMachine.Phase;

        // Progress sampling + stuck detection (Pattern D).
        // Only meaningful while actively working.
        if (phase == ShiftPhase.Working)
        {
            SampleProgress(currentLocation);
            // Re-read phase — SampleProgress may have triggered a transition.
            phase = _ctx.StateMachine.Phase;
            if (phase != ShiftPhase.Working)
                return; // let the new intent dispatch next tick
        }

        // Hit-reaction watcher (Pattern H) — independent of work state.
        CheckHitReaction();

        // Dispatch on current intent.
        switch (_ctx.StateMachine.CurrentIntent)
        {
            case IntentMoveToTile:
                HandleMovement(currentLocation);
                break;
            case IntentPerformTaskAt intent:
                HandleTaskAction(intent, currentLocation);
                break;
            case IntentPetAnimal intent:
                HandlePetAnimal(intent);
                break;
            case IntentCollectFromAnimal intent:
                HandleCollectFromAnimal(intent);
                break;
            case IntentPlayEmote intent:
                HandlePlayEmote(intent, currentLocation);
                break;
            case IntentTeleportToTile intent:
                HandleTeleportToTile(intent, currentLocation);
                break;
            case IntentTeleportHome:
                HandleTeleportHome(farm);
                break;
            case IntentDepositInShippingBin:
            case IntentDepositAtChest:
                HandleDeposit(farm);
                break;
            case IntentExitFarm:
                HandleExit(farm);
                break;
        }
    }

    // Called by CalendarHandlers.OnSavingHook when the player sleeps. v1 treats sleep as a hard stop:
    // no remaining tasks run headlessly, but collected/undelivered items are settled (mailed) before
    // contracts persist and the day rolls over. U-21 BR-SLEEP-02 removed refund semantics from this path.
    public void StopForSleepAndSettle()
    {
        if (_ctx is null)
        {
            ClearWorker();
            return;
        }

        FlushPendingDebrisSweeps();

        if (!_ctx.ShiftEndTime.HasValue)
        {
            _ctx.ShiftEndTime = Game1.timeOfDay;
            ModEntry.ModMonitor.Log(
                $"[Dayswork] Player slept during active shift; stopping worker at {_ctx.ShiftEndTime}.",
                LogLevel.Info);
        }

        // Mail every collected-but-undelivered item next morning; do not run remaining tasks or dump to bin.
        AppendUndeliveredToOverflow();

        _ctx.StateMachine.RegisterStopReason(ShiftStopReason.Sleep);
        SettleShiftMail();

        ClearWorker();
        _ctx = null;
    }

    public void OnTimeChanged(object? sender, TimeChangedEventArgs e)
    {
        if (_ctx is null) return;
        var phase = _ctx.StateMachine.Phase;

        // 8pm hard cap (BR-12 / HardCapTime).
        // Only fires from Working or Recovering — both have Depositing as a valid successor.
        // Stuck is transient (emote fires immediately) and resolves to Recovering within one tick.
        if (e.NewTime >= _config.HardCapTime &&
            (phase == ShiftPhase.Working || phase == ShiftPhase.Recovering))
        {
            ModEntry.ModMonitor.Log("[Dayswork] 8pm cap reached.", LogLevel.Trace);
            if (IsWorkUnitInProgress())
            {
                RequestBoundaryStop(ShiftStopReason.HardCap, e.NewTime);
            }
            else
            {
                QueueWrapUpNow(ShiftStopReason.HardCap, e.NewTime);
            }
        }
    }

    // ── Progress sampling / stuck detection (Pattern D) ──────────────────────

    private void SampleProgress(GameLocation location)
    {
        if (_farmhand is null) return;

        var currentTile = _farmhand.TilePoint;

        // FD-Q3=A: progress = tile moved OR action in progress.
        bool madeProgress = _actionPending || currentTile != _lastTilePos;
        _lastTilePos = currentTile;

        // Compute elapsed in-game minutes since last sample.
        // Game.timeOfDay advances in 10-unit steps (e.g. 600 → 610 = 10 in-game minutes).
        int elapsedMinutes = (Game1.timeOfDay - _lastSampledGameTime) / 10;
        _lastSampledGameTime = Game1.timeOfDay;

        _stuck.RecordTick(madeProgress, elapsedMinutes);

        if (_stuck.ShouldFireStuck())
            BeginStuckEscalation(location);
    }

    // ── Stuck escalation (Patterns D / E) ────────────────────────────────────

    /// <summary>
    /// Step 1: transition Working → Stuck with a "?" emote intent.
    /// HandlePlayEmote (called next tick) drives step 2 or 3 via QueueStuckTeleport.
    /// </summary>
    private void BeginStuckEscalation(GameLocation _)
    {
        if (_currentAnimalWork is not null)
        {
//             ModEntry.ModMonitor.Log(
//                 $"[Dayswork][animal] skipping unreachable animal {_currentAnimalWork.Animal.DisplayName} ({_currentAnimalWork.Task}).",
//                 LogLevel.Trace);
            _currentAnimalWork = null;
            StartNextAnimalOrTileOrAdvance();
            return;
        }

        _ctx!.StateMachine.Transition(ShiftPhase.Stuck, new IntentPlayEmote(EmoteQuestion));
    }

    private void HandlePlayEmote(IntentPlayEmote intent, GameLocation location)
    {
        _farmhand!.doEmote(intent.EmoteId);
        QueueStuckTeleport(location);
    }

    /// <summary>
    /// Decides step 2 vs step 3 of escalation and transitions Stuck → Recovering.
    /// Step 2: teleport to next reachable work tile (RecoveryAttempts == 0 and tile found).
    /// Step 3: teleport home and end shift early (RecoveryAttempts >= 1 or no reachable tile).
    /// </summary>
    private void QueueStuckTeleport(GameLocation location)
    {
        // Step 3 trigger: already attempted one recovery.
        if (_ctx!.RecoveryAttempts >= 1)
        {
            _ctx.StateMachine.Transition(ShiftPhase.Recovering, new IntentTeleportHome());
            return;
        }

        // Find the next reachable active-batch work tile.
        TileCoord? recoveryTile = TrySelectNextActiveWork(location, out _, out var selectedRoute)
            ? selectedRoute.InteractionTile
            : null;

        if (recoveryTile is null)
        {
            // No reachable tile — skip straight to step 3 (REL-U13-02).
            _ctx.StateMachine.Transition(ShiftPhase.Recovering, new IntentTeleportHome());
        }
        else
        {
            _ctx.StateMachine.Transition(ShiftPhase.Recovering, new IntentTeleportToTile(recoveryTile.Value));
        }
    }

    private void HandleTeleportToTile(IntentTeleportToTile intent, GameLocation location)
    {
        // Instant reposition to recovery tile, then resume working.
        _farmhand!.Position = new Vector2(intent.Destination.X, intent.Destination.Y) * 64f;
        _farmhand.currentLocation = location;
        _currentLocation = location;
        _nav.Clear();

        // Switch to post-teleport threshold and reset detector.
        _stuck = new StuckDetector(_config.StuckPostTeleportWaitMinutes);
        _lastSampledGameTime = Game1.timeOfDay;
        _lastTilePos         = _farmhand!.TilePoint;
        _ctx!.RecoveryAttempts++;

        // Recovering → Working: continue from the teleport tile.
        _actionPending = false;
        // The next work item drives the real nav through route-ranked active-batch selection.
        StartNextAnimalOrTileOrAdvance();
    }

    private void HandleTeleportHome(Farm farm)
    {
        // Step 3: reposition home and end shift via normal Depositing path (SAFE-U13-01).
        _farmhand!.Position = new Vector2(_farmExitTile.X, _farmExitTile.Y) * 64f;
        _farmhand.currentLocation = farm;
        _currentLocation = farm;
        _nav.Clear();
        QueueWrapUpNow(ShiftStopReason.StuckAbort);
    }

    // ── Hit-reaction watcher (Pattern H / BR-INVULN-01/02) ───────────────────

    private void CheckHitReaction()
    {
        if (_farmhand is null || _ctx is null) return;

        bool isSwinging = Game1.player.UsingTool && Game1.player.CurrentTool is MeleeWeapon;
        float dist = Math.Abs(_farmhand.TilePoint.X - Game1.player.TilePoint.X)
                   + Math.Abs(_farmhand.TilePoint.Y - Game1.player.TilePoint.Y);
        if (HitReactionPolicy.ShouldTriggerEmote(isSwinging, _playerWasSwinging, dist, HitRangeTiles))
            _farmhand.doEmote(EmoteExclamation);

        _playerWasSwinging = isSwinging;
    }

    // ── Movement handler ─────────────────────────────────────────────────────

    private void HandleMovement(GameLocation location)
    {
        if (_pendingExpansionRouteKind != PendingExpansionRouteKind.None)
        {
            HandleExpansionRouteMovement();
            return;
        }

        if (_nav.NavigationFailed)
        {
            if (_pendingBuildingExit)
            {
                ModEntry.ModMonitor.Log(
                    $"[Dayswork][building] could not walk to interior exit at ({_pendingInteriorExitTile.X},{_pendingInteriorExitTile.Y}); warping out.",
                    LogLevel.Warn);
                FinishCurrentBatchAfterBuildingExit();
                return;
            }

            if (_pendingBuildingEntry)
            {
                _buildingNavigator.LogSkipped(_pendingBuildingInterior?.Name ?? "unknown");
                _pendingBuildingEntry = false;
                _pendingBuildingInterior = null;
                _ctx!.CurrentBatchIndex++;
                BeginCurrentBatch();
                return;
            }

            if (_currentAnimalWork is not null)
            {
//                 ModEntry.ModMonitor.Log(
//                     $"[Dayswork][animal] navigation failed for {_currentAnimalWork.Animal.DisplayName} ({_currentAnimalWork.Task}); deferring within active batch.",
//                     LogLevel.Trace);
                _deferredAnimalWork.Add(_currentAnimalWork);
                _currentAnimalWork = null;
                StartNextAnimalOrTileOrAdvance();
                return;
            }

            if (_currentTileWork is not null)
            {
//                 ModEntry.ModMonitor.Log(
//                     $"[Dayswork][nav] failed task={_currentTileWork.Task} nav=({_pendingNavTile.X},{_pendingNavTile.Y}) task=({_currentTileWork.TaskTile.X},{_currentTileWork.TaskTile.Y}); deferring within active batch.",
//                     LogLevel.Trace);
                _deferredTileWork.Add(_currentTileWork);
                _currentTileWork = null;
                StartNextAnimalOrTileOrAdvance();
                return;
            }

            ModEntry.ModMonitor.Log(
                $"[Dayswork][nav] failed task={_pendingTask} nav=({_pendingNavTile.X},{_pendingNavTile.Y}) task=({_pendingTaskTile.X},{_pendingTaskTile.Y}); skipping.",
                LogLevel.Warn);
            AdvanceWorkList(location);
            return;
        }

        if (_nav.HasArrived)
        {
            if (_pendingBuildingExit)
            {
                FinishCurrentBatchAfterBuildingExit();
                return;
            }

            if (_pendingBuildingEntry)
            {
                CompleteBuildingEntry();
                return;
            }

            if (_currentAnimalWork is not null)
            {
                var animal = _animalHandler.FindLiveAnimal(location, _currentAnimalWork.Animal);
                if (animal is null || !IsAnimalWorkActionable(_currentAnimalWork, animal))
                {
                    _currentAnimalWork = null;
                    StartNextAnimalOrTileOrAdvance();
                    return;
                }

                _ctx!.StateMachine.SetIntent(_currentAnimalWork.Task == TaskKind.PetAnimals
                    ? new IntentPetAnimal(_currentAnimalWork.Animal)
                    : new IntentCollectFromAnimal(_currentAnimalWork.Animal));
                return;
            }

            if (_currentTileWork is not null && !IsTileWorkActionable(_currentTileWork, location))
            {
                _currentTileWork = null;
                StartNextAnimalOrTileOrAdvance();
                return;
            }

//             ModEntry.ModMonitor.Log(
//                 $"[Dayswork][nav] arrived task={_pendingTask} nav=({_pendingNavTile.X},{_pendingNavTile.Y}) task=({_pendingTaskTile.X},{_pendingTaskTile.Y}) worker=({_farmhand!.TilePoint.X},{_farmhand.TilePoint.Y}) fallback={_nav.UsedDirectFallback}.",
//                 LogLevel.Trace);
            _ctx!.StateMachine.SetIntent(new IntentPerformTaskAt(_pendingTaskTile, _pendingTask));
            _actionPending = false;
        }
    }

    private void HandleTaskAction(IntentPerformTaskAt intent, GameLocation location)
    {
        // Don't start a new swing while a felled tree is still falling — the trunk's fall
        // animation must finish before the stump is choppable. Swinging now wastes energy
        // and plays a phantom chop (most visible at fast animation speeds). The deferred
        // beat keeps the worker idle until falling clears, then chopping resumes.
        if (!_actionPending
            && intent.Task == TaskKind.CutTrees
            && IsCutTreeTargetFalling(intent.Tile, location))
            return;

        if (!_actionPending)
        {
            _toolAnimator.StopSwing();
            _toolAnimator.PlaySwing(intent.Task, FacingToward(_farmhand!.TilePoint, intent.Tile, _farmhand.FacingDirection));
//             ModEntry.ModMonitor.Log(
//                 $"[Dayswork][action] invoke task={intent.Task} taskTile=({intent.Tile.X},{intent.Tile.Y}) worker=({_farmhand.TilePoint.X},{_farmhand.TilePoint.Y}).",
//                 LogLevel.Trace);
            _pendingBeatOutcome = InvokeTaskActionGuarded(intent.Tile, intent.Task, location);
            SpendStaminaForBeat(ActionKindForTask(intent.Task));
            _actionPending = true;
            return;
        }

        if (_toolAnimator.IsSwinging)
            return;

        var outcome = _pendingBeatOutcome ?? new LaborBeatOutcome(true, IsTaskComplete(intent.Tile, intent.Task, location));
        _pendingBeatOutcome = null;
        _actionPending = false;

        var boundary = _boundaryClassifier.EvaluateAfterBeat(
            outcome.UnitResolved,
            _ctx!.EnergyState,
            HasBoundaryStopRequested());

        if (boundary.CanContinueCurrentUnit)
            return;

        if (boundary.ShouldWrapUpAfterCurrentUnit)
        {
            QueueWrapUpNow(_ctx.PendingStopReason ?? ShiftStopReason.Exhausted);
            return;
        }

        if (outcome.TaskFullyComplete)
        {
//             ModEntry.ModMonitor.Log(
//                 $"[Dayswork][action] complete task={intent.Task} taskTile=({intent.Tile.X},{intent.Tile.Y}).",
//                 LogLevel.Trace);
            AdvanceWorkList(location);
        }
    }

    private void HandlePetAnimal(IntentPetAnimal intent)
    {
        var location = _currentLocation ?? Game1.getFarm();
        var animal = _animalHandler.FindLiveAnimal(location, intent.Animal);
        if (animal is null)
        {
            _actionPending = false;
            _currentAnimalWork = null;
            FinishResolvedAnimalWork(location, madeProgress: false);
            return;
        }

        if (!_actionPending)
        {
            _toolAnimator.StopSwing();
            _toolAnimator.PlaySwing(WorkerTool.None,
                FacingToward(_farmhand!.TilePoint, _animalHandler.CurrentTile(animal), _farmhand.FacingDirection));
            SpendStaminaForBeat(WorkActionKind.PetAnimal);
            _actionPending = true;
            return;
        }

        if (_toolAnimator.IsSwinging)
            return;

        _animalHandler.Pet(animal);
        _actionPending = false;
        _currentAnimalWork = null;
        FinishResolvedAnimalWork(location);
    }

    private void HandleCollectFromAnimal(IntentCollectFromAnimal intent)
    {
        var location = _currentLocation ?? Game1.getFarm();
        var animal = _animalHandler.FindLiveAnimal(location, intent.Animal);
        if (animal is null)
        {
            _actionPending = false;
            _currentAnimalWork = null;
            FinishResolvedAnimalWork(location, madeProgress: false);
            return;
        }

        if (!_actionPending)
        {
            _toolAnimator.StopSwing();
            var collectTool = AnimalTaskHandler.IsShearProduce(animal) ? WorkerTool.Shears
                            : AnimalTaskHandler.IsMilkProduce(animal)  ? WorkerTool.MilkPail
                            : WorkerTool.None;
            _toolAnimator.PlaySwing(collectTool,
                FacingToward(_farmhand!.TilePoint, _animalHandler.CurrentTile(animal), _farmhand.FacingDirection));
            PlayAnimalCollectSound(location, collectTool);
            SpendStaminaForBeat(WorkActionKind.CollectAnimalProduct);
            _actionPending = true;
            return;
        }

        if (_toolAnimator.IsSwinging)
            return;

        _animalHandler.TryCollect(animal, _ctx!.Buffer, _currentAnimalWork?.Provenance ?? _pendingOutputProvenance);
        _actionPending = false;
        _currentAnimalWork = null;
        FinishResolvedAnimalWork(location);
    }

    private static void PlayAnimalCollectSound(GameLocation location, WorkerTool collectTool)
    {
        location.playSound(AnimalCollectAudioCue.ForTool(collectTool));
    }

    // ── Deposit / exit handlers ───────────────────────────────────────────────

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
        // already mail the items inside BeginTripExecution and return false so we skip the loop.
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

    // Starts walking the worker to the nearest reachable interior exit door after an
    // interior-chest deposit. Returns false (no walk) when the trip was a farm/shipping-bin
    // destination or the worker is already on the farm. Mirrors CompleteCurrentBatch's exit.
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

    // Drain the current trip's bookkeeping, dequeue the next trip (or exit if none).
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

    // Returns true if the trip is ready to tick beats; false if the trip was aborted
    // (chest missing or busy) and all its stacks were already routed to overflow.
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
                // Chest moved/destroyed (FR-OUT-03): everything for it mails (ChestMissing).
                foreach (var stack in trip.Items)
                    _ctx!.Overflow.Add(new OverflowItem(stack, OverflowReason.ChestMissing));
                ModEntry.ModMonitor.Log(
                    $"[Dayswork][deposit] chest missing at ({chestDest.Ref.Tile.X},{chestDest.Ref.Tile.Y}); {trip.Items.Count} stack(s) → mail.",
                    LogLevel.Trace);
                return false;
            }

            if (chest.GetMutex().IsLocked())
            {
                // A farmer (player) has the chest UI open. Defer the whole trip to mail
                // rather than mutating items behind the player's back.
                foreach (var stack in trip.Items)
                    _ctx!.Overflow.Add(new OverflowItem(stack, OverflowReason.ChestBusy));
                ModEntry.ModMonitor.Log(
                    $"[Dayswork][deposit] chest busy at ({chestDest.Ref.Tile.X},{chestDest.Ref.Tile.Y}); {trip.Items.Count} stack(s) → mail.",
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
            // middle of our deposit, abort the rest of the trip and mail what remains.
            if (chest.GetMutex().IsLocked())
            {
                for (var i = _currentTripStackIndex; i < _currentTrip.Items.Count; i++)
                    _ctx.Overflow.Add(new OverflowItem(_currentTrip.Items[i], OverflowReason.ChestBusy));
                _currentTripStackIndex = _currentTrip.Items.Count;  // skip ahead to "trip complete"
                ModEntry.ModMonitor.Log(
                    $"[Dayswork][deposit] chest became busy mid-trip; remaining stacks → mail.",
                    LogLevel.Trace);
                return;
            }

            DepositIntoChest(chest, stack);
            if (playerHere && chest.Location is { } chestLoc)
                chestLoc.playSound("Ship", new Vector2(chest.TileLocation.X, chest.TileLocation.Y));
            return;
        }

        // Shipping bin path.
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

        if (playerHere)
            farm.shipItem(item, Game1.player);          // vanilla lid animation + backpackIN + delayed "Ship"
        else
            farm.getShippingBin(Game1.player).Add(item); // silent fallback
    }

    // ItemRegistry.Create(badId) returns SDV's fallback Error Item (Name="Error Item",
    // QualifiedItemId="(O)") rather than null when the id can't be resolved. This guard
    // refuses to deposit Error Items into a chest/bin — those would otherwise survive into
    // the player's world as "(O)" placeholders.
    private static bool IsDepositErrorItem(Item item) =>
        item is null
        || string.IsNullOrEmpty(item.ItemId)
        || item.QualifiedItemId == "(O)"
        || string.Equals(item.Name, "Error Item", StringComparison.Ordinal);

    private void EndTripExecution()
    {
        if (_currentTripChest is { } chest && _currentTripChestAnimated)
        {
            // Vanilla close trigger: the chest's per-tick update will animate the lid down
            // and emit the "doorClose" sound on completion.
            chest.frameCounter.Value = -1;
        }
    }

    // Best-effort facing toward the chest/bin tile. Falls back to the worker's current facing.
    private int FacingTowardDestination()
    {
        if (_currentTrip is null || _farmhand is null)
            return _farmhand?.FacingDirection ?? 2;
        return FacingToward(_farmhand.TilePoint, _currentTrip.Tile, _farmhand.FacingDirection);
    }

    private void MarkDepositTripUndelivered(DepositTrip trip)
    {
        foreach (var stack in trip.Items)
            _ctx!.Overflow.Add(new OverflowItem(stack, OverflowReason.NotDelivered));

        ModEntry.ModMonitor.Log(
            $"[Dayswork][deposit] could not reach deposit destination at ({trip.Tile.X},{trip.Tile.Y}); mailing {trip.Items.Count} stack(s).",
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
            // Chest full (FR-OUT-02): mail the remainder (ChestFull).
            _ctx!.Overflow.Add(new OverflowItem(
                new RoutedItemStack(stack.QualifiedItemId, leftover.Stack, stack.SourceTask, stack.Provenance),
                OverflowReason.ChestFull));
            ModEntry.ModMonitor.Log(
                $"[Dayswork][deposit] chest full; {leftover.Stack}x {stack.QualifiedItemId} → mail.",
                LogLevel.Trace);
        }
    }

    private static ShiftIntent ToDepositIntent(DepositTrip trip) =>
        trip.Destination is ChestDestination cd
            ? new IntentDepositAtChest(cd.Ref)
            : new IntentDepositInShippingBin();

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
            LogLevel.Info);

        // One settlement letter next morning for overflow items only; U-21 removes refund settlement.
        SettleShiftMail();

        ClearWorker();
        _ctx.StateMachine.Transition(ShiftPhase.Done);
        _ctx = null;
    }

    // ── Work list helpers ─────────────────────────────────────────────────────

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
            ShippingBinTile,
            workerTile,
            Manhattan);

        // Items resolved straight to mail are seeded into the overflow set (Pattern O / FD-Q2=A).
        foreach (var stack in plan.PreMailedOverflow)
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

    // Cross the building door (warp into the interior) and start walking to the chest.
    // Called from HandleDeposit once the worker has reached the outdoor door tile.
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

    // ── Overflow / mail helpers (Pattern O) ───────────────────────────────────

    // One settlement letter per shift carrying any overflow items. Sends nothing when there are
    // no overflow items (Pattern U). U-21 BR-END-03 removed the shift-end refund settlement —
    // the player already paid the contract price for the day.
    private void SettleShiftMail()
    {
        if (_ctx is null) return;

        IReadOnlyList<ItemStack> items = _ctx.Overflow.Count > 0
            ? ConsolidateOverflow(_ctx.Overflow)
            : Array.Empty<ItemStack>();
        var categories = _overflowCategorizer.Categorize(_ctx.Overflow);

        _mailDispatcher.QueueSettlement(items, categories);
        _ctx.Overflow.Clear();
    }

    private static IReadOnlyList<ItemStack> ConsolidateOverflow(IEnumerable<OverflowItem> overflow)
    {
        var totals = new Dictionary<string, int>();
        foreach (var o in overflow)
            totals[o.Stack.QualifiedItemId] =
                totals.TryGetValue(o.Stack.QualifiedItemId, out var e) ? e + o.Stack.Quantity : o.Stack.Quantity;
        return totals
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => new ItemStack(kv.Key, kv.Value))
            .ToList();
    }

    // Moves everything still undelivered (buffer + in-flight trip + queued trips) into the overflow
    // set as NotDelivered, so a save mid-cleanup loses nothing (FD-Q5=A / BR-INT-01).
    private void AppendUndeliveredToOverflow()
    {
        if (_ctx is null) return;

        foreach (var b in _ctx.Buffer.TakeAll())
            _ctx.Overflow.Add(new OverflowItem(
                new RoutedItemStack(b.QualifiedItemId, b.Quantity, b.SourceTask, b.Provenance),
                OverflowReason.NotDelivered));

        if (_currentTrip is not null)
        {
            foreach (var s in _currentTrip.Items)
                _ctx.Overflow.Add(new OverflowItem(s, OverflowReason.NotDelivered));
            _currentTrip = null;
        }

        while (_depositTrips.Count > 0)
            foreach (var s in _depositTrips.Dequeue().Items)
                _ctx.Overflow.Add(new OverflowItem(s, OverflowReason.NotDelivered));
    }

    private void ClearWorker()
    {
        if (_farmhand is not null)
        {
            _farmhand.controller = null;
            (_farmhand.currentLocation ?? _currentLocation)?.characters.Remove(_farmhand);
            if (Context.IsWorldReady)
                Game1.getFarm().characters.Remove(_farmhand);
        }

        _toolAnimator.SetWorker(null);
        _farmhand = null;
        _currentLocation = null;
        _actionPending = false;
        _morningEntranceHoldTicks = 0;
        _pendingTask = default;
        _pendingNavTile = default;
        _pendingTaskTile = default;
        _pendingOutputProvenance = OutputScopeProvenance.Unknown;
        _waitingForDebrisBeforeDeposit = false;
        _pendingBuildingEntry = false;
        _pendingBuildingExit = false;
        _pendingBuildingOutdoorDoor = default;
        _pendingBuildingInterior = null;
        _pendingInteriorExitTile = default;
        _currentFeedPlan = null;
        _hayInHand = 0;
        _animalWork.Clear();
        _deferredTileWork.Clear();
        _deferredAnimalWork.Clear();
        _activeBatchSelectionAttempts = 0;
        _currentTileWork = null;
        _currentAnimalWork = null;
        _pendingBeatOutcome = null;
        _pendingDebrisSweeps.Clear();
        _depositTrips.Clear();
        _currentTrip = null;
        _currentTripExecutionStarted = false;
        _currentTripStackIndex = 0;
        _currentTripChest = null;
        _currentTripLocation = null;
        _currentTripChestAnimated = false;
        _pendingDepositInterior = null;
        _pendingDepositExit = false;
        _pendingExpansionRouteKind = PendingExpansionRouteKind.None;
        _pendingExpansionRouteBatch = null;
        _expansionRouteNavigator.Clear();
        _nav.Clear();
    }

    private static int FacingToward(Point from, TileCoord to, int fallback)
    {
        var dx = to.X - from.X;
        var dy = to.Y - from.Y;

        if (dx == 0 && dy == 0)
            return fallback;

        if (Math.Abs(dx) > Math.Abs(dy))
            return dx > 0 ? 1 : 3;

        return dy > 0 ? 2 : 0;
    }

    private static bool IsTileReachable(TileCoord tile, GameLocation location) =>
        WorkerMovementDriver.IsTilePassableForWorker(new Point(tile.X, tile.Y), location);

    // ── Task invocation (Invoke-and-Poll) ─────────────────────────────────────

    private LaborBeatOutcome InvokeTaskActionGuarded(TileCoord tile, TaskKind task, GameLocation location)
    {
        // Some vanilla worker-facing callbacks still mutate Game1.player directly
        // (crop harvest, HUD/item gain flows, etc.) even though the worker is taking
        // the action. Snapshot the player's transient action state and restore it
        // immediately after the worker beat so no farmer animation leaks through.
        var playerState = new Game1WorkerActionPlayerState(Game1.player);
        var savedState = WorkerActionPlayerStateSnapshot.Capture(playerState);

        // Snapshot the HUD message queue so any item-pickup notifications fired by vanilla
        // harvest/grass/tree APIs (e.g. via Farmer.addItemToInventory → OnItemReceived) can
        // be removed below. The worker's items end up in _ctx.Buffer, never the player's
        // inventory, so the player should never see those notifications.
        var hudMessageCountBefore = Game1.hudMessages.Count;

        try
        {
            return InvokeTaskAction(tile, task, location);
        }
        finally
        {
            var playerStateChanged = savedState.DiffersFrom(playerState);
            var changedStateDescription = playerStateChanged
                ? WorkerActionPlayerStateSnapshot.Describe(playerState)
                : "";

            savedState.Restore(playerState);
            var restoredStateDescription = playerStateChanged
                ? WorkerActionPlayerStateSnapshot.Describe(playerState)
                : "";

            // Trim any HUD messages enqueued during the worker beat. The beat runs
            // synchronously inside OnUpdateTicked, so nothing else queues messages
            // here — appended entries can only come from the vanilla worker-side
            // callbacks we just invoked.
            while (Game1.hudMessages.Count > hudMessageCountBefore)
                Game1.hudMessages.RemoveAt(Game1.hudMessages.Count - 1);

            if (playerStateChanged)
                LogWorkerActionPlayerStateRestore(task, tile, location, savedState.Describe(), changedStateDescription, restoredStateDescription);
        }
    }

    private static void LogWorkerActionPlayerStateRestore(
        TaskKind task,
        TileCoord tile,
        GameLocation location,
        string savedState,
        string changedState,
        string restoredState)
    {
        ModEntry.ModMonitor.Log(
            $"[Dayswork][player-action-guard] Worker task {task} at ({tile.X},{tile.Y}) in {location.NameOrUniqueName} changed Game1.player action state while playerTool={DescribePlayerTool(Game1.player)}; restored. saved={{ {savedState} }} changed={{ {changedState} }} restored={{ {restoredState} }}",
            LogLevel.Debug);
    }

    private static string DescribePlayerTool(Farmer player)
    {
        var tool = player.CurrentTool;
        if (tool is null)
            return "<none>";

        return $"{tool.GetType().Name}:{tool.Name ?? "<unnamed>"}:{tool.QualifiedItemId ?? "<no-id>"}";
    }

    private LaborBeatOutcome InvokeTaskAction(TileCoord tile, TaskKind task, GameLocation location)
    {
        return task switch
        {
            TaskKind.WaterCrops => InvokeWater(tile, location),
            TaskKind.HarvestCrops => InvokeHarvest(tile, location),
            TaskKind.CollectFruit => InvokeCollectFruit(tile, location),
            TaskKind.CollectAnimalProducts => InvokeCollectAnimalProduct(tile, location),
            TaskKind.FeedAnimals => InvokeFeedAnimal(tile, location),
            TaskKind.ClearWeeds => InvokeClearWeed(tile, location),
            TaskKind.ClearGrass => InvokeClearGrass(tile, location),
            TaskKind.ClearRocks => InvokeClearRock(tile, location),
            TaskKind.CutTrees => InvokeCutTree(tile, location),
            _ => new LaborBeatOutcome(true, true),
        };
    }

    private static LaborBeatOutcome InvokeWater(TileCoord tile, GameLocation loc)
    {
        if (loc.terrainFeatures.TryGetValue(new Vector2(tile.X, tile.Y), out var tf) && tf is HoeDirt dirt)
            dirt.state.Value = HoeDirt.watered;
        return new LaborBeatOutcome(true, true);
    }

    private LaborBeatOutcome InvokeHarvest(TileCoord tile, GameLocation loc)
    {
        var tileVec = new Vector2(tile.X, tile.Y);
        if (!loc.terrainFeatures.TryGetValue(tileVec, out var tf) || tf is not HoeDirt dirt || dirt.crop is null)
            return new LaborBeatOutcome(true, true);

        var before = new HashSet<Debris>(loc.debris);

        // SDV 1.6: crop.harvest() adds produce directly to Game1.player (or creates debris that
        // the player magnet instantly collects). Snapshot player inventory by reference+stack so we
        // can intercept any gain and redirect it to the worker buffer instead.
        var inventoryBefore = Game1.player.Items
            .Where(i => i is not null)
            .ToDictionary(i => i, i => i.Stack);

        dirt.crop.harvest(tile.X, tile.Y, dirt, null);

        // Redirect any items that landed in the player's inventory to the worker buffer.
        foreach (var item in Game1.player.Items.Where(i => i is not null).ToList())
        {
            if (!inventoryBefore.TryGetValue(item, out var oldStack))
            {
                // Brand-new slot added by harvest — take the whole stack.
                _ctx!.Buffer.Add(item.QualifiedItemId, item.Stack, TaskKind.HarvestCrops, _pendingOutputProvenance);
                Game1.player.removeItemFromInventory(item);
            }
            else if (item.Stack > oldStack)
            {
                // Existing slot grew (stacked onto items the player already had).
                var gain = item.Stack - oldStack;
                _ctx!.Buffer.Add(item.QualifiedItemId, gain, TaskKind.HarvestCrops, _pendingOutputProvenance);
                item.Stack -= gain;
                if (item.Stack <= 0)
                    Game1.player.removeItemFromInventory(item);
            }
        }

        // crop.harvest() does not clean up dirt.crop — vanilla expects the caller to do it.
        // For non-regrowable crops call HoeDirt.destroyCrop (the authoritative cleanup path).
        // Regrowable crops stay on the dirt and start regrowing; HoeDirt.readyForHarvest()
        // will return false until they're ready again, so IsTaskComplete terminates naturally.
        // Note: watering of regrowable crops is handled by a separate WaterCrops WorkItem
        // emitted by WorkAreaScanner after the harvest item, so the worker plays a distinct
        // watering animation instead of silently flipping dirt.state here.
        if (dirt.crop is not null && !dirt.crop.RegrowsAfterHarvest())
            dirt.destroyCrop(false);

        // Also collect any debris-spawned items (in case harvest used the debris path).
        CollectNewDebrisAtTile(before, loc, _pendingTask, tileVec, _pendingOutputProvenance);
        return new LaborBeatOutcome(true, true);
    }

    private LaborBeatOutcome InvokeCollectFruit(TileCoord tile, GameLocation loc)
    {
        var tileVec = new Vector2(tile.X, tile.Y);
        if (!loc.terrainFeatures.TryGetValue(tileVec, out var tf) || tf is not FruitTree tree)
            return new LaborBeatOutcome(true, true);
        var before = new HashSet<Debris>(loc.debris);
        var hadFruit = tree.fruit.Count > 0;
        tree.shake(tileVec, false);
        CollectNewDebrisAtTile(before, loc, _pendingTask, tileVec, _pendingOutputProvenance);
        // Shaken fruit settles over the next several beats, so an immediate sweep misses it.
        // Queue a delayed sweep (same mechanism trees use for falling wood) to catch it.
        if (hadFruit)
            QueueDelayedDebrisSweep(loc, tileVec, before, _pendingTask, _pendingOutputProvenance);
        return new LaborBeatOutcome(true, true);
    }

    private LaborBeatOutcome InvokeCollectAnimalProduct(TileCoord tile, GameLocation loc)
    {
        var tileVec = new Vector2(tile.X, tile.Y);
        if (!loc.objects.TryGetValue(tileVec, out var obj) ||
            !WorkAreaScanner.IsAnimalProductForageObject(obj))
            return new LaborBeatOutcome(true, true);

        _ctx!.Buffer.Add(obj.QualifiedItemId, Math.Max(1, obj.Stack), TaskKind.CollectAnimalProducts, _pendingOutputProvenance);
        loc.removeObject(tileVec, false);
        return new LaborBeatOutcome(true, true);
    }

    private LaborBeatOutcome InvokeFeedAnimal(TileCoord tile, GameLocation loc)
    {
        if (_currentFeedPlan is null)
            return new LaborBeatOutcome(true, true);

        if (tile == _currentFeedPlan.HopperTile && _hayInHand <= 0)
        {
            if (_animalHandler.TakeHay(loc, _currentFeedPlan.HayToTake))
                _hayInHand = _currentFeedPlan.HayToTake;
            return new LaborBeatOutcome(true, true);
        }

        if (_hayInHand <= 0)
            return new LaborBeatOutcome(true, true);

        if (_animalHandler.PlaceHay(loc, tile))
            _hayInHand--;
        return new LaborBeatOutcome(true, true);
    }

    private LaborBeatOutcome InvokeClearWeed(TileCoord tile, GameLocation loc)
    {
        var tileVec = new Vector2(tile.X, tile.Y);
        if (!loc.objects.TryGetValue(tileVec, out var obj) || !obj.IsWeeds())
            return new LaborBeatOutcome(true, true);
        var before = new HashSet<Debris>(loc.debris);
        var scythe = new MeleeWeapon("66") { lastUser = CreateWorkerActionFarmer(tile, loc) };
        obj.performToolAction(scythe);
        if (loc.objects.ContainsKey(tileVec))
            loc.removeObject(tileVec, false);
        CollectNewDebrisAtTile(before, loc, _pendingTask, tileVec, _pendingOutputProvenance);
        return new LaborBeatOutcome(true, true);
    }

    private LaborBeatOutcome InvokeClearGrass(TileCoord tile, GameLocation loc)
    {
        var tileVec = new Vector2(tile.X, tile.Y);
        if (!loc.terrainFeatures.TryGetValue(tileVec, out var tf) || tf is not Grass grass)
            return new LaborBeatOutcome(true, true);
        var scythe = new MeleeWeapon("66") { lastUser = CreateWorkerActionFarmer(tile, loc) };
        grass.performToolAction(scythe, 0, tileVec);
        if (loc.terrainFeatures.ContainsKey(tileVec))
            loc.terrainFeatures.Remove(tileVec);
        return new LaborBeatOutcome(true, true);
    }

    private LaborBeatOutcome InvokeClearRock(TileCoord tile, GameLocation loc)
    {
        var tileVec = new Vector2(tile.X, tile.Y);
        var pickaxe = new Pickaxe { UpgradeLevel = (int)_ctx!.ToolSnapshot.PickaxeLevel, lastUser = CreateWorkerActionFarmer(tile, loc) };

        if (ObjectTargetClassifier.FindResourceClumpAt(tileVec, loc) is { } clump)
        {
            // performToolAction applies tool-level damage per swing:
            //   Math.Max(1, (upgradeLevel + 1) * 0.75)
            // The clump's health persists between action cycles, so the action loop
            // naturally requires multiple swings — tool quality determines how many.
            // When health reaches 0, performToolAction calls destroy() internally to
            // spawn all loot drops, then returns true. Never call destroy() again.
            var beforeClump = new HashSet<Debris>(loc.debris);
            var destroyed   = clump.performToolAction(pickaxe, 0, clump.Tile);
            CollectNewDebrisAtTile(beforeClump, loc, _pendingTask, clump.Tile, _pendingOutputProvenance);

            if (destroyed)
                loc.resourceClumps.Remove(clump);
            // If not destroyed, IsTaskComplete still finds the clump → action loop re-fires.
            return new LaborBeatOutcome(destroyed, destroyed);
        }

        if (!loc.objects.TryGetValue(tileVec, out var obj)) return new LaborBeatOutcome(true, true);
        if (ObjectTargetClassifier.ClassifyPick(tileVec, loc) is null) return new LaborBeatOutcome(true, true);

        var before  = new HashSet<Debris>(loc.debris);
        var actionRemoved = obj.performToolAction(pickaxe);
        if (loc.objects.ContainsKey(tileVec))
            loc.removeObject(tileVec, false);
        var removed = !loc.objects.ContainsKey(tileVec);
//         ModEntry.ModMonitor.Log(
//             $"[Dayswork][action] clear rock at ({tile.X},{tile.Y}) performToolAction={actionRemoved} removed={removed}.",
//             LogLevel.Trace);
        var collectedDebris = CollectNewDebrisAtTile(before, loc, _pendingTask, tileVec, _pendingOutputProvenance);
        if (!collectedDebris && removed && TryGetRemovedStandardStoneDrop(obj, out var itemId, out var stack))
        {
            _ctx.Buffer.Add(itemId, stack, _pendingTask, _pendingOutputProvenance);
//             ModEntry.ModMonitor.Log(
//                 $"[Dayswork][debris] collected {stack}x {itemId} from removed standard stone object task={_pendingTask}.",
//                 LogLevel.Trace);
        }

        return new LaborBeatOutcome(true, true);
    }

    // True while the tree at this tile is mid-fall (trunk falling animation after the
    // felling hit). Hits during this window deal no damage, so the beat loop should wait.
    private static bool IsCutTreeTargetFalling(TileCoord tile, GameLocation loc)
    {
        var tileVec = new Vector2(tile.X, tile.Y);
        return loc.terrainFeatures.TryGetValue(tileVec, out var tf)
            && tf is Tree tree
            && tree.falling.Value;
    }

    private LaborBeatOutcome InvokeCutTree(TileCoord tile, GameLocation loc)
    {
        var tileVec = new Vector2(tile.X, tile.Y);
        var axe = new Axe { UpgradeLevel = (int)_ctx!.ToolSnapshot.AxeLevel, lastUser = CreateWorkerActionFarmer(tile, loc) };

        if (loc.terrainFeatures.TryGetValue(tileVec, out var tf) && tf is Tree tree)
        {
            bool wasStump   = tree.stump.Value;
            var  before     = new HashSet<Debris>(loc.debris);
            var  removeTree = tree.performToolAction(axe, 0, tileVec);
            if (removeTree && loc.terrainFeatures.ContainsKey(tileVec))
                loc.terrainFeatures.Remove(tileVec);
//             ModEntry.ModMonitor.Log(
//                 $"[Dayswork][action] cut tree at ({tile.X},{tile.Y}) remove={removeTree} health={tree.health.Value:0.##} stump={tree.stump.Value}.",
//                 LogLevel.Trace);
            CollectNewDebrisAtTile(before, loc, _pendingTask, tileVec, _pendingOutputProvenance);
            if (!wasStump && !removeTree)
                QueueDelayedDebrisSweep(loc, tileVec, before, _pendingTask, _pendingOutputProvenance);

            if (!wasStump && tree.stump.Value)
                return new LaborBeatOutcome(true, false);

            return new LaborBeatOutcome(removeTree, removeTree);
        }

        if (ObjectTargetClassifier.FindResourceClumpAt(tileVec, loc) is { } clump)
        {
            var beforeClump = new HashSet<Debris>(loc.debris);
            var destroyed   = clump.performToolAction(axe, 0, clump.Tile);
            CollectNewDebrisAtTile(beforeClump, loc, _pendingTask, clump.Tile, _pendingOutputProvenance);
            if (destroyed)
                loc.resourceClumps.Remove(clump);
            return new LaborBeatOutcome(destroyed, destroyed);
        }

        if (loc.objects.TryGetValue(tileVec, out var obj) && obj.Name == "Twig")
        {
            var before = new HashSet<Debris>(loc.debris);
            obj.performToolAction(axe);
            if (loc.objects.ContainsKey(tileVec))
                loc.removeObject(tileVec, false);
            CollectNewDebrisAtTile(before, loc, _pendingTask, tileVec, _pendingOutputProvenance);
            return new LaborBeatOutcome(true, true);
        }

        return new LaborBeatOutcome(true, true);
    }

    private Farmer CreateWorkerActionFarmer(TileCoord taskTile, GameLocation location)
    {
        var actionFarmer = Game1.player.CreateFakeEventFarmer();
        actionFarmer.currentLocation = location;
        actionFarmer.Position = _farmhand?.Position ?? Game1.player.Position;
        actionFarmer.faceDirection(
            FacingToward(
                _farmhand?.TilePoint ?? Game1.player.TilePoint,
                taskTile,
                _farmhand?.FacingDirection ?? Game1.player.FacingDirection));
        actionFarmer.CanMove = false;
        actionFarmer.UsingTool = false;
        actionFarmer.canReleaseTool = false;
        actionFarmer.jitterStrength = 0f;
        actionFarmer.FarmerSprite.pauseForSingleAnimation = false;
        actionFarmer.FarmerSprite.StopAnimation();
        return actionFarmer;
    }

    private bool CollectNewDebrisAtTile(
        HashSet<Debris> before,
        GameLocation loc,
        TaskKind sourceTask,
        Vector2 tileVec,
        OutputScopeProvenance provenance) =>
        CollectNewDebris(
            before,
            loc,
            sourceTask,
            new Vector2(tileVec.X * 64f + 32f, tileVec.Y * 64f + 32f),
            ImmediateDebrisSweepRadiusTiles,
            provenance);

    private bool CollectNewDebris(
        HashSet<Debris> before,
        GameLocation loc,
        TaskKind sourceTask,
        Vector2? origin = null,
        int radiusTiles = int.MaxValue,
        OutputScopeProvenance? provenance = null)
    {
        bool collected = false;
        foreach (var d in loc.debris.ToList())
        {
            if (before.Contains(d) ||
                (origin.HasValue && !IsDebrisNear(d, origin.Value, radiusTiles)))
                continue;

            if (!TryGetDebrisItem(d, out var itemId, out var stack))
            {
                LogInvalidDebris(loc, sourceTask, origin, d);
                continue;
            }

            _ctx!.Buffer.Add(itemId, stack, sourceTask, provenance ?? OutputScopeProvenance.Unknown);
//             ModEntry.ModMonitor.Log(
//                 $"[Dayswork][debris] collected {stack}x {itemId} from game debris task={sourceTask} chunks={d.Chunks.Count} debrisType={d.debrisType.Value} chunkType={d.chunkType.Value}.",
//                 LogLevel.Trace);
            loc.debris.Remove(d);
            collected = true;
        }
        return collected;
    }

    private static bool TryGetDebrisItem(Debris debris, out string itemId, out int stack)
    {
        if (debris.item is not null)
        {
            stack = Math.Max(1, debris.item.Stack);
            return DebrisItemIdResolver.TryResolveCollectibleItemId(debris.item.QualifiedItemId, out itemId);
        }

        var debrisItemId = debris.itemId.Value;
        if (DebrisItemIdResolver.TryResolveCollectibleItemId(debrisItemId, out itemId))
        {
            stack = debris.debrisType.Value == Debris.DebrisType.RESOURCE
                ? Math.Max(1, debris.Chunks.Count)
                : 1;
            return true;
        }

        itemId = "";
        stack = 0;
        return false;
    }

    private static void LogInvalidDebris(GameLocation loc, TaskKind sourceTask, Vector2? origin, Debris debris)
    {
        if (debris.item is null && string.IsNullOrWhiteSpace(debris.itemId.Value))
            return;

        var rawItemId = debris.item?.QualifiedItemId ?? debris.itemId.Value ?? "";
        var rawDisplayName = debris.item?.DisplayName ?? "<none>";
        var originText = origin.HasValue
            ? $"({(int)(origin.Value.X / 64f)},{(int)(origin.Value.Y / 64f)})"
            : "<none>";

        ModEntry.ModMonitor.Log(
            $"[Dayswork][debris] worker-created debris could not be resolved to a valid item id raw='{rawItemId}' display='{rawDisplayName}' loc={loc.Name} task={sourceTask} origin={originText} chunks={debris.Chunks.Count} debrisType={debris.debrisType.Value} chunkType={debris.chunkType.Value}.",
            LogLevel.Warn);
    }

    private static bool TryGetRemovedStandardStoneDrop(StardewValley.Object obj, out string itemId, out int stack)
    {
        if (obj.QualifiedItemId == "(O)390" || obj.ItemId == "390" || obj.Name == "Stone")
        {
            itemId = "(O)390";
            stack = 1;
            return true;
        }

        itemId = "";
        stack = 0;
        return false;
    }

    private void QueueDelayedDebrisSweep(
        GameLocation loc,
        Vector2 tileVec,
        HashSet<Debris> baseline,
        TaskKind sourceTask,
        OutputScopeProvenance provenance)
    {
        var origin = new Vector2(tileVec.X * 64f + 32f, tileVec.Y * 64f + 32f);
        _pendingDebrisSweeps.Add(new PendingDebrisSweep(
            loc,
            origin,
            baseline,
            DelayedTreeDebrisSweepTicks,
            DelayedTreeDebrisSweepRadiusTiles,
            sourceTask,
            provenance));
    }

    private void ProcessPendingDebrisSweeps()
    {
        for (var i = _pendingDebrisSweeps.Count - 1; i >= 0; i--)
        {
            var sweep = _pendingDebrisSweeps[i];
            CollectNewDebris(sweep.Baseline, sweep.Location, sweep.SourceTask, sweep.Origin, sweep.RadiusTiles, sweep.Provenance);
            sweep.TicksRemaining--;
            if (sweep.TicksRemaining <= 0)
                _pendingDebrisSweeps.RemoveAt(i);
        }
    }

    private void FlushPendingDebrisSweeps()
    {
        foreach (var sweep in _pendingDebrisSweeps)
            CollectNewDebris(sweep.Baseline, sweep.Location, sweep.SourceTask, sweep.Origin, sweep.RadiusTiles, sweep.Provenance);

        _pendingDebrisSweeps.Clear();
    }

    private static bool IsDebrisNear(Debris debris, Vector2 origin, int radiusTiles)
    {
        var radiusPixels = radiusTiles * 64f;
        var radiusSq = radiusPixels * radiusPixels;

        foreach (var chunk in debris.Chunks)
        {
            if (Vector2.DistanceSquared(chunk.position.Value, origin) <= radiusSq)
                return true;
        }

        return false;
    }

    // ── Completion detection ──────────────────────────────────────────────────

    private static bool IsTaskComplete(TileCoord tile, TaskKind task, GameLocation loc)
    {
        var tileVec = new Vector2(tile.X, tile.Y);
        return task switch
        {
            TaskKind.WaterCrops =>
                !loc.terrainFeatures.TryGetValue(tileVec, out var tf1) ||
                tf1 is not HoeDirt d || d.state.Value == HoeDirt.watered,

            TaskKind.HarvestCrops =>
                !loc.terrainFeatures.TryGetValue(tileVec, out var tf2) ||
                tf2 is not HoeDirt hd ||
                hd.crop is null ||
                !hd.readyForHarvest(),

            TaskKind.CollectFruit =>
                !loc.terrainFeatures.TryGetValue(tileVec, out var tf3) ||
                tf3 is not FruitTree ft || ft.fruit.Count == 0,

            TaskKind.CollectAnimalProducts =>
                !loc.objects.TryGetValue(tileVec, out var product) ||
                !WorkAreaScanner.IsAnimalProductForageObject(product),

            TaskKind.FeedAnimals => true,

            TaskKind.ClearWeeds or TaskKind.ClearRocks =>
                !loc.objects.ContainsKey(tileVec) &&
                ObjectTargetClassifier.FindResourceClumpAt(tileVec, loc) is null,

            TaskKind.ClearGrass =>
                !loc.terrainFeatures.ContainsKey(tileVec),

            TaskKind.CutTrees =>
                !loc.terrainFeatures.ContainsKey(tileVec) &&
                (!loc.objects.TryGetValue(tileVec, out var obj) || obj.Name != "Twig") &&
                ObjectTargetClassifier.FindResourceClumpAt(tileVec, loc) is null,

            _ => true,
        };
    }

    private sealed class PendingDebrisSweep
    {
        public PendingDebrisSweep(
            GameLocation location,
            Vector2 origin,
            HashSet<Debris> baseline,
            int ticksRemaining,
            int radiusTiles,
            TaskKind sourceTask,
            OutputScopeProvenance provenance)
        {
            Location = location;
            Origin = origin;
            Baseline = baseline;
            TicksRemaining = ticksRemaining;
            RadiusTiles = radiusTiles;
            SourceTask = sourceTask;
            Provenance = provenance;
        }

        public GameLocation Location { get; }
        public Vector2 Origin { get; }
        public HashSet<Debris> Baseline { get; }
        public int TicksRemaining { get; set; }
        public int RadiusTiles { get; }
        public TaskKind SourceTask { get; }
        public OutputScopeProvenance Provenance { get; }
    }

    private sealed record ActiveWorkCandidate(
        WorkItem? TileWork,
        AnimalWorkItem? AnimalWork,
        TaskKind Task,
        TileCoord TaskTile,
        IReadOnlyList<TileCoord> NavigationTiles,
        int StableOrder);

    private sealed record LaborBeatOutcome(bool UnitResolved, bool TaskFullyComplete);
}
