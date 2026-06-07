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
using StardewValley.Menus;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;

namespace Dayswork.Orchestration;

internal sealed partial class ShiftOrchestrator : ISessionBoundaryResettable
{
    private enum PendingExpansionRouteKind
    {
        None,
        WorkEntry,
        WorkExit,
        DepositEntry,
        DepositExit,
    }

    // Fallback shipping-bin stand tile for Standard Farm when the live building cannot be resolved.
    private static readonly TileCoord FallbackShippingBinTile = new(71, 13);

    // Emote IDs — play-test TODO: confirm "?" and "!" are 8 and 2 in vanilla.
    // See code-summary.md play-test checklist.
    private const int EmoteQuestion    = 8;  // confused "?" (stuck step 1)
    private const int EmoteExclamation = 2;  // surprised "!" (hit reaction)
    private const int EmoteMusic       = 16; // music note while waiting for a shop to open

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
    private readonly IShiftOutcomeDispatcher _shiftOutcomeDispatcher;
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
    private TileCoord     _currentExitTile;

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
        IShiftOutcomeDispatcher shiftOutcomeDispatcher)
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
        _shiftOutcomeDispatcher = shiftOutcomeDispatcher;
        _expansionRouteNavigator = new CrossLocationRouteNavigator(nav);
        _stuck             = new StuckDetector(config.StuckInitialWaitMinutes);
    }

    private static int Manhattan(TileCoord a, TileCoord b) =>
        Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);

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

    public ContractId? ActiveContractId => _ctx?.ContractId;

    public void EndShiftEarly()
    {
        if (_ctx is null)
        {
            ModEntry.ModMonitor.Log("[Dayswork] No active shift to cancel.", LogLevel.Trace);
            return;
        }

        var phase = _ctx.StateMachine.Phase;
        if (phase is ShiftPhase.Depositing or ShiftPhase.Exiting or ShiftPhase.Done)
        {
            ModEntry.ModMonitor.Log("[Dayswork] Shift is already finishing — nothing to cancel.", LogLevel.Trace);
            return;
        }

        ModEntry.ModMonitor.Log("[Dayswork] Player cancelled shift early — worker will deposit and leave.", LogLevel.Trace);
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
                LogLevel.Trace);
        }

        ClearWorker();
        _ctx = null;
        _tickCount = 0;
        _farmExitTile = default;
        _currentExitTile = default;
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
        var workScopes = _scopeClassifier.Classify(runtimeScopeSelection, contract.EnabledTasks, contract.CropPlan);

        DevLog.Log(
            $"[Dayswork][managed-crops] StartShift cropPlan enabled={contract.CropPlan.IsEnabled} assignments={contract.CropPlan.Assignments.Count} " +
            $"managedScope={(workScopes.ManagedCrops?.Assignments.Count ?? -1)} zoneLocations=[{string.Join(", ", contract.CropPlan.Assignments.Select(a => a.Zone.LocationName))}].",
            LogLevel.Info);

        _farmExitTile = ResolveSpawnExitTile(farm);
        var batches = BuildInitialBatches(contract, workScopes, farm, snapshot);

        if (batches.Count == 0 ||
            batches.All(batch => batch.Kind is BatchKind.OutdoorAnimals or BatchKind.OutdoorCrops or BatchKind.OutdoorClearing or BatchKind.FarmForage &&
                                 batch.TileWork.Count == 0 &&
                                 batch.AnimalWork.Count == 0 &&
                                 !batch.FeedBuilding))
        {
            ModEntry.ModMonitor.Log("[Dayswork] No applicable work found for today's contract — no worker spawned.", LogLevel.Trace);
            DevLog.Log(
                $"[Dayswork][managed-crops] no-worker guard fired. batches={batches.Count} kinds=[{string.Join(", ", batches.Select(b => $"{b.Kind}:{b.LocationName}"))}] managedScope={(workScopes.ManagedCrops?.Assignments.Count ?? -1)}.",
                LogLevel.Info);
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
        ResetManagedCropState();
        _shopStockReader.ResetForShift();
        Dayswork.Integration.CropHudNotifier.ResetForShift();

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

        Game1.addHUDMessage(new HUDMessage(
            I18nHelper.Get("notify.shift_started", new { price = contract.TermsSnapshot.Pricing.TotalPrice }),
            HUDMessage.newQuest_type));

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

    private IReadOnlyList<WorkBatch> BuildInitialBatches(
        Contract contract,
        WorkScopeSet workScopes,
        Farm farm,
        ToolSnapshot snapshot)
    {
        var skeletons = _shiftPlanBuilder.BuildBatchPlan(workScopes, contract.EnabledTasks);
        var outdoorZones = workScopes.OutdoorWork?.NormalizedZones ?? Array.Empty<Zone>();
        var outdoorProvenance = OutputScopeProvenance.Outdoor();

        // Coexistence (U-MC-05, FR-MC-28): tiles owned by a managed crop zone on the farm are
        // serviced by the managed-crop batch, so exclude them from the general outdoor scans.
        var managedFarmZones = (workScopes.ManagedCrops?.Assignments ?? Array.Empty<Dayswork.Core.Crops.CropZoneAssignment>())
            .Where(assignment => string.Equals(assignment.Zone.LocationName, "Farm", StringComparison.Ordinal))
            .Select(assignment => assignment.Zone)
            .ToList();
        var greenhouseLocation = workScopes.GreenhouseWork?.LocationName ?? "Greenhouse";

        var batches = new List<WorkBatch>(skeletons.Count);
        foreach (var batch in skeletons)
        {
            switch (batch.Kind)
            {
                case BatchKind.AnimalBuilding:
                case BatchKind.Greenhouse:
                case BatchKind.ManagedCrops:
                    // ManagedCrops carries no TileWork; the managed-crop runner reads its zone
                    // assignments from WorkScopeSet.ManagedCrops at batch start.
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
                            outdoorProvenance,
                            managedFarmZones);
                    batches.Add(batch with { TileWork = tileWork });
                    break;
                }

                default:
                    throw new ArgumentOutOfRangeException(nameof(batch.Kind), batch.Kind, null);
            }
        }

        DevLog.Log(
            $"[Dayswork][shift-plan] runtime batches={string.Join(", ", batches.Select(batch => $"{batch.Kind}:{batch.LocationName}:{string.Join("/", batch.Tasks)}"))} greenhouse={greenhouseLocation} outdoorTiles={outdoorZones.Count}.");
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
        if (IsManagedCropBatch(batch))
        {
            BeginManagedCropBatch(batch);
            return;
        }

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

        DevLog.Log($"[Dayswork][building-grazing] home={batch.LocationName} animalWork={refreshedAnimalWork.Count}.");
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

        DevLog.Log($"[Dayswork][farm-forage] tileWork={refreshedTileWork.Count}.");
        return batch with { TileWork = refreshedTileWork, AnimalWork = Array.Empty<AnimalWorkItem>() };
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
        if (phase == ShiftPhase.Working && !_managedShoppingInProgress)
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
            case IntentPerformManagedCropAction intent:
                HandleManagedCropAction(intent, currentLocation);
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
                LogLevel.Trace);
        }

        // Route collected-but-undelivered items through their safe final destination; explicit
        // shipping-bin output goes straight to the bin, while all other undelivered output uses
        // automatic overflow.
        SettleManagedShoppingCarriedItems(showHud: false);
        AppendUndeliveredToOverflow();

        _ctx.StateMachine.RegisterStopReason(ShiftStopReason.Sleep);
        DispatchShiftOverflow();

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
            if (_managedShoppingInProgress)
            {
                RequestBoundaryStop(ShiftStopReason.HardCap, e.NewTime);
            }
            else if (IsWorkUnitInProgress())
            {
                RequestBoundaryStop(ShiftStopReason.HardCap, e.NewTime);
            }
            else
            {
                QueueWrapUpNow(ShiftStopReason.HardCap, e.NewTime);
            }
        }
    }

    private static ShiftIntent ToDepositIntent(DepositTrip trip) =>
        trip.Destination is ChestDestination cd
            ? new IntentDepositAtChest(cd.Ref)
            : new IntentDepositInShippingBin();

    private static TileCoord ResolveShippingBinDepositTile(Farm farm)
    {
        foreach (var building in farm.buildings)
        {
            if (!string.Equals(building.buildingType.Value, "Shipping Bin", StringComparison.OrdinalIgnoreCase))
                continue;

            var door = building.getPointForHumanDoor();
            return ResolveShippingBinApproachTile(farm, new TileCoord(door.X, door.Y));
        }

        return FallbackShippingBinTile;
    }

    private static TileCoord ResolveShippingBinApproachTile(Farm farm, TileCoord doorTile)
    {
        TileCoord[] candidates =
        {
            doorTile,
            new(doorTile.X, doorTile.Y + 1),
            new(doorTile.X - 1, doorTile.Y + 1),
            new(doorTile.X + 1, doorTile.Y + 1),
            new(doorTile.X - 1, doorTile.Y),
            new(doorTile.X + 1, doorTile.Y),
            new(doorTile.X, doorTile.Y - 1),
        };

        foreach (var candidate in candidates)
        {
            if (WorkerMovementDriver.IsTilePassableForWorker(new Point(candidate.X, candidate.Y), farm))
                return candidate;
        }

        return doorTile;
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

    private static DestinationKey ResolveAssignedDestination(
        TaskKind task,
        IReadOnlyDictionary<TaskKind, DestinationKey> assignments) =>
        assignments.TryGetValue(task, out var destination) && destination is not null
            ? destination
            : AutomaticOutputDestination.Instance;

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
        ResetManagedCropState();
        _nav.Clear();
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
