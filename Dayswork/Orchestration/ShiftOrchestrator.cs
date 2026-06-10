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
    // Fallback shipping-bin stand tile for Standard Farm when the live building cannot be resolved.
    private static readonly TileCoord FallbackShippingBinTile = new(71, 13);

    // Emote IDs — play-test TODO: confirm "?" and "!" are 8 and 2 in vanilla.
    private const int EmoteQuestion    = 8;  // confused "?" (stuck step 1)
    private const int EmoteExclamation = 2;  // surprised "!" (hit reaction)
    internal const int EmoteMusic      = 16; // music note while waiting for a shop to open

    // Melee proximity range for hit-detection (Manhattan distance in tiles).
    private const float HitRangeTiles = 2.0f;

    // Brief morning hold so the player sees the worker enter from the farm entrance.
    // Vanilla tree debris can spawn after the tree-fall animation, not on the axe-hit tick.
    private const int ImmediateDebrisSweepRadiusTiles = 3;
    private const int DelayedTreeDebrisSweepTicks = 240;
    private const int DelayedTreeDebrisSweepRadiusTiles = 6;

    private readonly ToolLevelReader      _toolReader;
    private readonly ToolSwapAnimator     _toolAnimator;
    private readonly ShiftPlanBuilder     _shiftPlanBuilder = new();
    private readonly WorkScopeClassifier _scopeClassifier;
    private ConfigSnapshot               _config;
    private readonly WorkerMovementDriver _nav;
    private readonly WorkAreaScanner      _workAreaScanner;
    private readonly IndoorWorkScanner    _indoorScanner;
    private readonly AnimalTaskHandler    _animalHandler;
    private readonly BuildingWorkNavigator _buildingNavigator;
    private readonly ChestResolver        _chestResolver;
    private readonly DepositPlanner      _depositPlanner;
    private readonly IShiftOutcomeDispatcher _shiftOutcomeDispatcher;
    private readonly WorkerEnergyLedger _energyLedger = new();
    private readonly WorkUnitBoundaryClassifier _boundaryClassifier = new();
    private readonly OverflowCategorizer _overflowCategorizer = new();
    private readonly WorkerRouteSelector _routeSelector = new();
    private readonly TravelRunner _travel;

    // The one nullable per-shift reference: all mutable shift state lives on the session
    // (created at StartShift once spawn succeeds, discarded when the shift ends). Code that
    // runs only during a shift uses the throwing accessor below.
    private ShiftSession? _session;
    private ShiftSession Session =>
        _session ?? throw new InvalidOperationException("No active shift session.");

    public ShiftOrchestrator(
        ToolLevelReader toolReader,
        ConfigSnapshot config,
        WorkScopeClassifier scopeClassifier,
        ToolSwapAnimator toolAnimator,
        WorkerMovementDriver nav,
        WorkAreaScanner workAreaScanner,
        IndoorWorkScanner indoorScanner,
        AnimalTaskHandler animalHandler,
        BuildingWorkNavigator buildingNavigator,
        ChestResolver chestResolver,
        DepositPlanner depositPlanner,
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
        _travel            = new TravelRunner(nav);
    }

    private static int Manhattan(TileCoord a, TileCoord b) =>
        Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);

    private bool HasBoundaryStopRequested() => _session?.Ctx.PendingStopReason is not null;

    private bool IsWorkUnitInProgress() => Session.ActionPending;

    internal bool ShouldWrapUpBeforeNextUnit() =>
        _session is not null && (Session.Ctx.PendingStopReason is not null || !Session.Ctx.EnergyState.CanStartNewWorkUnit);

    private void RequestBoundaryStop(ShiftStopReason reason, int? stopTime = null)
    {
        if (_session is null)
            return;

        Session.Ctx.PendingStopReason ??= reason;
        Session.Ctx.ShiftEndTime ??= stopTime ?? Game1.timeOfDay;
    }

    internal void QueueWrapUpNow(ShiftStopReason reason, int? stopTime = null)
    {
        if (_session is null)
            return;

        RequestBoundaryStop(reason, stopTime);
        BeginDeposit();
    }

    private void SpendStaminaForBeat(WorkActionKind action)
    {
        if (_session is null)
            return;

        var spendResult = _energyLedger.ApplyActionCost(Session.Ctx.EnergyState, action);
        Session.Ctx.EnergyState = spendResult.State;
        Session.Worker?.SetStamina(Session.Ctx.EnergyState.RemainingEnergy, Session.Ctx.EnergyState.Capacity);
        if (spendResult.ReachedZeroOnThisBeat)
            RequestBoundaryStop(ShiftStopReason.Exhausted);
    }

    private void FinishResolvedAnimalWork(GameLocation location, bool madeProgress = true)
    {
        if (_session is null)
            return;

        var boundary = _boundaryClassifier.EvaluateAfterBeat(
            unitResolved: true,
            energyState: Session.Ctx.EnergyState,
            boundaryStopRequested: HasBoundaryStopRequested());

        if (boundary.ShouldWrapUpAfterCurrentUnit)
        {
            QueueWrapUpNow(Session.Ctx.PendingStopReason ?? ShiftStopReason.Exhausted);
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

    public ContractId? ActiveContractId => _session?.Ctx.ContractId;

    public void EndShiftEarly()
    {
        if (_session is null)
        {
            ModEntry.ModMonitor.Log("[Dayswork] No active shift to cancel.", LogLevel.Trace);
            return;
        }

        var phase = Session.Ctx.StateMachine.Phase;
        if (phase is ShiftPhase.Depositing or ShiftPhase.Exiting or ShiftPhase.Done)
        {
            ModEntry.ModMonitor.Log("[Dayswork] Shift is already finishing — nothing to cancel.", LogLevel.Trace);
            return;
        }

        ModEntry.ModMonitor.Log("[Dayswork] Player cancelled shift early — worker will deposit and leave.", LogLevel.Trace);
        Session.Ctx.ShiftEndTime = Game1.timeOfDay;

        // Stuck is a transient working state; bridge it to Recovering so that
        // BeginDeposit (which transitions Recovering → Depositing) is legal.
        if (phase == ShiftPhase.Stuck)
            Session.Ctx.StateMachine.Transition(ShiftPhase.Recovering, new IntentTeleportHome());

        QueueWrapUpNow(ShiftStopReason.Cancelled);
    }

    public void ResetForSessionBoundary(SessionResetBoundary boundary)
    {
        var hadRuntimeState = _session is not null;

        if (hadRuntimeState)
        {
            ModEntry.ModMonitor.Log(
                $"[Dayswork] Resetting in-memory worker runtime for session boundary {boundary}.",
                LogLevel.Trace);
        }

        DespawnWorker();
        _session = null;
    }

    public void StartShift(Contract contract, ConfigSnapshot runtimeConfig)
    {
        if (_session is not null)
        {
            ModEntry.ModMonitor.Log("[Dayswork] StartShift called while a shift is already active — ignoring.", LogLevel.Warn);
            return;
        }

        _config = runtimeConfig;
        var priorityOrderer = new TaskPriorityOrderer(contract.CategoryPriority);
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

        // Farm exit warp tile — computed once per shift from farm.warps (not a static constant,
        // because the warp tile varies by farm type and player map edits).
        var farmExitTile = ResolveSpawnExitTile(farm);
        var batches = BuildInitialBatches(contract, workScopes, farm, snapshot, farmExitTile, priorityOrderer);

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

        var spawnPos = new Vector2(farmExitTile.X, farmExitTile.Y) * 64f;
        var farmhand = new FarmhandNpc(spawnPos);
        farm.addCharacter(farmhand);
        _toolAnimator.SetWorker(farmhand);
        _toolAnimator.SetPacingProfile(pacingProfile);
        _nav.SetPacingProfile(pacingProfile);
        farmhand.SetStamina(energyState.RemainingEnergy, energyState.Capacity);

        // Reset the shift-scoped pieces that outlive a session.
        _travel.Clear();
        _shopStockReader.ResetForShift();
        Dayswork.Integration.CropHudNotifier.ResetForShift();

        var ctx = new ShiftContext(
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

        _session = new ShiftSession(
            ctx,
            farmhand,
            farm,
            farmExitTile,
            priorityOrderer,
            new StuckDetector(_config.StuckInitialWaitMinutes))
        {
            LastSampledGameTime = Game1.timeOfDay,
            LastTilePos = farmhand.TilePoint,
            MorningEntranceHoldTicks = pacingProfile.EntranceHoldTicks,
        };
        _session.Shopping = new ManagedShoppingCoordinator(
            _session,
            this,
            _nav,
            _toolAnimator,
            _cropFieldReader,
            _shiftSupplyAggregator,
            _shopStockReader,
            _purchaseAffordability,
            _shopPurchaseService);
        _session.Deposits = new DepositTripRunner(
            _session,
            this,
            _nav,
            _toolAnimator,
            _chestResolver,
            _buildingNavigator);

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
        ToolSnapshot snapshot,
        TileCoord farmExitTile,
        TaskPriorityOrderer priorityOrderer)
    {
        var skeletons = _shiftPlanBuilder.BuildBatchPlan(workScopes, contract.EnabledTasks);
        var outdoorZones = workScopes.OutdoorWork?.NormalizedZones ?? Array.Empty<Zone>();
        var outdoorProvenance = OutputScopeProvenance.Outdoor();

        // Coexistence: tiles owned by a managed crop zone are serviced by
        // the managed-crop batch for that live location, so general crop scans exclude only the
        // zones that match the active scan location.
        var managedZonesByLocation = (workScopes.ManagedCrops?.Assignments ?? Array.Empty<Dayswork.Core.Crops.CropZoneAssignment>())
            .GroupBy(assignment => assignment.Zone.LocationName, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<Zone>)group.Select(assignment => assignment.Zone).ToList(),
                StringComparer.Ordinal);
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
                    // Per-building grazing pass: service only the grazing animals whose
                    // home key matches this batch's building. Farm-wide forage is handled separately
                    // by the trailing FarmForage batch.
                    var batchTasks = batch.Tasks.ToHashSet();
                    var buildingHomes = new HashSet<string>(StringComparer.Ordinal) { batch.LocationName };
                    var animalWork = BuildAnimalWork(farm, buildingHomes, batchTasks, priorityOrderer);
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
                        farmExitTile,
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
                            farmExitTile,
                            outdoorProvenance,
                            ManagedZonesForLocation(batch.LocationName));
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

        IReadOnlyList<Zone> ManagedZonesForLocation(string locationName) =>
            managedZonesByLocation.TryGetValue(locationName, out var zones)
                ? zones
                : Array.Empty<Zone>();
    }

    private IReadOnlyList<AnimalWorkItem> BuildAnimalWork(
        GameLocation location,
        IReadOnlySet<string> selectedAnimalHomes,
        IReadOnlySet<TaskKind> enabledTasks,
        TaskPriorityOrderer priorityOrderer)
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
            .SelectMany(group => group.OrderBy(item => priorityOrderer.Order(new[] { item.Task })[0]))
            .ToList();
    }

    private void BeginCurrentBatch()
    {
        if (_session is null || Session.Worker is null)
            return;

        Session.AnimalWork.Clear();
        Session.DeferredTileWork.Clear();
        Session.DeferredAnimalWork.Clear();
        Session.CurrentTileWork = null;
        Session.CurrentAnimalWork = null;
        Session.BatchSelectionAttempts = 0;
        Session.MaxBatchSelectionAttempts = 4;
        Session.CurrentFeedPlan = null;
        Session.HayInHand = 0;

        if (Session.Ctx.CurrentBatchIndex >= Session.Ctx.Batches.Count)
        {
            QueueWrapUpNow(ShiftStopReason.Completed);
            return;
        }

        var batch = Session.Ctx.Batches[Session.Ctx.CurrentBatchIndex];
        if (IsManagedCropBatch(batch))
        {
            if (!string.Equals(batch.LocationName, "Farm", StringComparison.Ordinal))
            {
                if (ModEntry.ExpansionCompat is { } compat &&
                    compat.TryGetExpansionLocationDescriptor(batch.LocationName, out var descriptor) &&
                    descriptor.Role == ExpansionLocationRole.GreenhouseWork)
                {
                    if (TryStartExpansionTravel(
                            "Farm",
                            batch.LocationName,
                            ExpansionRoutePurpose.WorkEntry,
                            TravelFailurePolicy.ReportFailure,
                            TravelPurpose.WorkEntry))
                        return;

                    Session.Ctx.CurrentBatchIndex++;
                    BeginCurrentBatch();
                    return;
                }

                if (TryBuildBuildingEntryPlan(batch.LocationName, TravelFailurePolicy.ReportFailure, out var plan))
                {
                    StartTravel(plan, TravelPurpose.WorkEntry);
                    return;
                }

                Session.Ctx.CurrentBatchIndex++;
                BeginCurrentBatch();
                return;
            }

            BeginManagedCropBatch(batch);
            return;
        }

        if (IsExpansionGreenhouseBatch(batch))
        {
            if (TryStartExpansionTravel(
                    "Farm",
                    batch.LocationName,
                    ExpansionRoutePurpose.WorkEntry,
                    TravelFailurePolicy.ReportFailure,
                    TravelPurpose.WorkEntry))
                return;

            Session.Ctx.CurrentBatchIndex++;
            BeginCurrentBatch();
            return;
        }

        if (BatchRequiresInteriorEntry(batch))
        {
            if (TryBuildBuildingEntryPlan(batch.LocationName, TravelFailurePolicy.ReportFailure, out var plan))
            {
                StartTravel(plan, TravelPurpose.WorkEntry);
                return;
            }

            Session.Ctx.CurrentBatchIndex++;
            BeginCurrentBatch();
            return;
        }

        Session.CurrentLocation = Game1.getFarm();
        if (batch.Kind == BatchKind.OutdoorAnimals)
            batch = RefreshBuildingGrazingWork(batch, Session.CurrentLocation);
        else if (batch.Kind == BatchKind.FarmForage)
            batch = RefreshFarmForageWork(batch, Session.CurrentLocation);
        QueueBatchWork(batch, Session.CurrentLocation);
        StartNextAnimalOrTileOrAdvance();
    }

    private WorkBatch RefreshBuildingGrazingWork(WorkBatch batch, GameLocation farm)
    {
        if (_session is null || batch.Kind != BatchKind.OutdoorAnimals)
            return batch;

        // Per-building grazing pass: rebuild this one building's grazing-animal work at
        // batch start (animals roam, so the shift-start snapshot is stale). Scoped to the single
        // building's home key; farm-wide forage is handled by the separate FarmForage batch.
        var buildingHomes = new HashSet<string>(StringComparer.Ordinal) { batch.LocationName };
        var batchTasks = batch.Tasks.ToHashSet();
        var refreshedAnimalWork = BuildAnimalWork(farm, buildingHomes, batchTasks, Session.PriorityOrderer);

        DevLog.Log($"[Dayswork][building-grazing] home={batch.LocationName} animalWork={refreshedAnimalWork.Count}.");
        return batch with { TileWork = Array.Empty<WorkItem>(), AnimalWork = refreshedAnimalWork };
    }

    private WorkBatch RefreshFarmForageWork(WorkBatch batch, GameLocation farm)
    {
        if (_session is null || batch.Kind != BatchKind.FarmForage)
            return batch;

        // Truffles spawn on the farm continuously through the day as pigs forage. The initial
        // shift-start scan in BuildInitialBatches is hours stale by the time we actually start this
        // final farm-wide pass, so re-scan for forage-style animal products here.
        var batchTasks = batch.Tasks.ToHashSet();
        var refreshedTileWork = batchTasks.Contains(TaskKind.CollectAnimalProducts)
            ? _workAreaScanner.ScanWholeLocation(
                farm,
                batchTasks,
                Session.Ctx.ToolSnapshot,
                Session.FarmExitTile,
                OutputScopeProvenance.AnimalBuilding(string.Empty))
            : (IReadOnlyList<WorkItem>)Array.Empty<WorkItem>();

        DevLog.Log($"[Dayswork][farm-forage] tileWork={refreshedTileWork.Count}.");
        return batch with { TileWork = refreshedTileWork, AnimalWork = Array.Empty<AnimalWorkItem>() };
    }

    private void CompleteCurrentBatch()
    {
        if (_session is null || Session.Worker is null)
            return;

        var batch = Session.Ctx.Batches[Session.Ctx.CurrentBatchIndex];
        if (IsExpansionGreenhouseBatch(batch))
        {
            var currentName = (Session.CurrentLocation ?? Session.Worker.currentLocation)?.NameOrUniqueName
                              ?? batch.LocationName;
            Session.Ctx.CurrentBatchIndex++;
            if (!string.Equals(currentName, "Farm", StringComparison.OrdinalIgnoreCase) &&
                TryStartExpansionTravel(
                    currentName,
                    "Farm",
                    ExpansionRoutePurpose.ReturnToFarm,
                    TravelFailurePolicy.WarpToDestination,
                    TravelPurpose.WorkExit))
                return;

            WarpExpansionWorkerToFarm();
            BeginCurrentBatch();
            return;
        }

        if (BatchRequiresInteriorEntry(batch))
        {
            var interior = Session.CurrentLocation;
            if (interior is not null && interior != Game1.getFarm())
            {
                var farmArrival = _buildingNavigator.TryResolveDoorTile(batch.LocationName, out var outdoorDoor, out _)
                    ? outdoorDoor
                    : Session.FarmExitTile;
                Session.Ctx.CurrentBatchIndex++;
                StartTravel(BuildBuildingExitPlan(interior, farmArrival), TravelPurpose.WorkExit);
                return;
            }
        }

        Session.Ctx.CurrentBatchIndex++;
        BeginCurrentBatch();
    }

    private void EnsureWorkingIntent(ShiftIntent intent)
    {
        if (_session is null)
            return;

        if (Session.Ctx.StateMachine.Phase == ShiftPhase.WaitingForSpawn ||
            Session.Ctx.StateMachine.Phase == ShiftPhase.Recovering)
        {
            Session.Ctx.StateMachine.Transition(ShiftPhase.Working, intent);
            return;
        }

        Session.Ctx.StateMachine.SetIntent(intent);
    }

    public void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
    {
        if (_session is null || Session.Ctx.StateMachine.Phase == ShiftPhase.Done) return;
        if (!Game1.shouldTimePass(false)) return;
        if (Session.MorningEntranceHoldTicks > 0 && Session.Ctx.StateMachine.Phase == ShiftPhase.Working)
        {
            Session.MorningEntranceHoldTicks--;
            return;
        }

        _toolAnimator.Update(Game1.currentGameTime);
        _nav.Update();
        ProcessPendingDebrisSweeps();
        if (Session.WaitingForDebrisBeforeDeposit)
        {
            if (Session.PendingDebrisSweeps.Count == 0)
            {
                Session.WaitingForDebrisBeforeDeposit = false;
                BeginDeposit();
            }
            return;
        }
        if (++Session.TickCount % 4 != 0) return; // tick throttle

        var farm  = Game1.getFarm();
        var currentLocation = Session.CurrentLocation ?? farm;
        var phase = Session.Ctx.StateMachine.Phase;

        // Progress sampling + stuck detection.
        // Only meaningful while actively working.
        if (phase == ShiftPhase.Working && !Session.Shopping.IsInProgress)
        {
            SampleProgress(currentLocation);
            // Re-read phase — SampleProgress may have triggered a transition.
            phase = Session.Ctx.StateMachine.Phase;
            if (phase != ShiftPhase.Working)
                return; // let the new intent dispatch next tick
        }

        // Hit-reaction watcher — independent of work state.
        CheckHitReaction();

        // Shopping wait loop: parked at the wait tile until the store opens (no travel active).
        if (Session.Shopping.IsWaitingForOpen)
        {
            Session.Shopping.ContinueWaitTick();
            return;
        }

        // An active travel owns the worker's movement until it completes or fails; its
        // completion handler restores the right intent for whatever comes next.
        if (Session.TravelPurpose != TravelPurpose.None)
        {
            HandleTravel();
            return;
        }

        // Dispatch on current intent.
        switch (Session.Ctx.StateMachine.CurrentIntent)
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
                Session.Deposits.HandleDepositTick(farm);
                break;
            case IntentExitFarm:
                HandleExit(farm);
                break;
        }
    }

    public void StopForSleepAndSettle()
    {
        if (_session is null)
        {
            DespawnWorker();
            return;
        }

        FlushPendingDebrisSweeps();

        var ctx = _session.Ctx;
        if (!ctx.ShiftEndTime.HasValue)
        {
            ctx.ShiftEndTime = Game1.timeOfDay;
            ModEntry.ModMonitor.Log(
                $"[Dayswork] Player slept during active shift; stopping worker at {ctx.ShiftEndTime}.",
                LogLevel.Trace);
        }

        // Route collected-but-undelivered items through their safe final destination; explicit
        // shipping-bin output goes straight to the bin, while all other undelivered output uses
        // automatic overflow. This must all happen BEFORE the session is discarded.
        _session.Shopping.SettleCarriedItems(showHud: false);
        _session.Deposits.AppendUndeliveredToOverflow();

        ctx.StateMachine.RegisterStopReason(ShiftStopReason.Sleep);
        DispatchShiftOverflow();

        DespawnWorker();
        _session = null;
    }

    public void OnTimeChanged(object? sender, TimeChangedEventArgs e)
    {
        if (_session is null) return;
        var phase = Session.Ctx.StateMachine.Phase;

        // 8pm hard cap (HardCapTime).
        // Only fires from Working or Recovering — both have Depositing as a valid successor.
        // Stuck is transient (emote fires immediately) and resolves to Recovering within one tick.
        if (e.NewTime >= _config.HardCapTime &&
            (phase == ShiftPhase.Working || phase == ShiftPhase.Recovering))
        {
            ModEntry.ModMonitor.Log("[Dayswork] 8pm cap reached.", LogLevel.Trace);
            if (Session.Shopping.IsInProgress)
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

    internal static ShiftIntent ToDepositIntent(DepositTrip trip) =>
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

    internal static DestinationKey ResolveAssignedDestination(
        TaskKind task,
        IReadOnlyDictionary<TaskKind, DestinationKey> assignments) =>
        assignments.TryGetValue(task, out var destination) && destination is not null
            ? destination
            : AutomaticOutputDestination.Instance;

    // Removes the NPC from the world and clears the shift-scoped state that lives on the
    // orchestrator rather than the session (travel runner, deposit trips, shopping runtime).
    // Every caller discards the session right after, which is the rest of the reset.
    private void DespawnWorker()
    {
        if (_session?.Worker is { } worker)
        {
            worker.controller = null;
            (worker.currentLocation ?? _session.CurrentLocation)?.characters.Remove(worker);
            if (Context.IsWorldReady)
                Game1.getFarm().characters.Remove(worker);
            _session.Worker = null;
            _session.CurrentLocation = null;
        }

        _toolAnimator.SetWorker(null);
        CancelActiveTravel();
        _nav.Clear();
    }

    private sealed record ActiveWorkCandidate(
        WorkItem? TileWork,
        AnimalWorkItem? AnimalWork,
        TaskKind Task,
        TileCoord TaskTile,
        IReadOnlyList<TileCoord> NavigationTiles,
        int StableOrder);

}
