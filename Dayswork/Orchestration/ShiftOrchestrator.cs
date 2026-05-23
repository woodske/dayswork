using Dayswork.Core.Config;
using Dayswork.Core.Domain;
using Dayswork.Core.Inventory;
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

internal sealed class ShiftOrchestrator
{
    // Farm entrance tile — worker spawns and exits here.
    private static readonly TileCoord FarmEntrance   = new(71, 14);
    private static readonly Vector2 FarmExitPastEntrancePixel = new(FarmEntrance.X * 64f, (FarmEntrance.Y + 2) * 64f);
    // Shipping bin tile on Standard Farm.
    private static readonly TileCoord ShippingBinTile = new(71, 13);

    // Emote IDs — play-test TODO: confirm "?" and "!" are 8 and 2 in vanilla.
    // See code-summary.md play-test checklist.
    private const int EmoteQuestion    = 8;  // confused "?" (stuck step 1)
    private const int EmoteExclamation = 2;  // surprised "!" (hit reaction)

    // Melee proximity range for hit-detection (Manhattan distance in tiles).
    private const float HitRangeTiles = 2.0f;

    // Brief morning hold so the player sees the worker enter from the farm entrance.
    private const int MorningEntranceHoldTicks = 120;

    // Vanilla tree debris can spawn after the tree-fall animation, not on the axe-hit tick.
    private const int ImmediateDebrisSweepRadiusTiles = 3;
    private const int DelayedTreeDebrisSweepTicks = 240;
    private const int DelayedTreeDebrisSweepRadiusTiles = 6;

    private readonly ToolLevelReader      _toolReader;
    private readonly ToolSwapAnimator     _toolAnimator;
    private readonly ITaskPriorityOrderer _priorityOrderer = new TaskPriorityOrderer();
    private readonly ShiftPlanBuilder     _shiftPlanBuilder = new();
    private IConfigSnapshot               _config;
    private readonly WorkerMovementDriver _nav;
    private readonly WorkAreaScanner      _workAreaScanner;
    private readonly IndoorWorkScanner    _indoorScanner;
    private readonly AnimalTaskHandler    _animalHandler;
    private readonly BuildingWorkNavigator _buildingNavigator;
    private readonly ChestResolver        _chestResolver;
    private readonly IDepositPlanner      _depositPlanner;
    private readonly IMailDispatcher      _mailDispatcher;

    private ShiftContext? _ctx;
    private FarmhandNpc?  _farmhand;
    private GameLocation? _currentLocation;
    private int           _tickCount;
    private int           _morningEntranceHoldTicks;
    private bool          _exitWalkStarted;

    // Multi-trip deposit loop state (Pattern N): the ordered remaining trips and the in-flight one.
    private readonly Queue<DepositTrip> _depositTrips = new();
    private DepositTrip? _currentTrip;

    // Per-WorkItem state — the nav tile and task tile are tracked separately (trellis crops).
    private bool      _actionPending;
    private TaskKind  _pendingTask;
    private TileCoord _pendingNavTile;
    private TileCoord _pendingTaskTile;
    private bool      _waitingForDebrisBeforeDeposit;
    private bool      _pendingBuildingEntry;
    private bool      _pendingBuildingExit;
    private TileCoord _pendingBuildingOutdoorDoor;
    private GameLocation? _pendingBuildingInterior;
    private TileCoord _pendingInteriorExitTile;
    private FeedWorkPlan? _currentFeedPlan;
    private int _hayInHand;

    private readonly Queue<AnimalWorkItem> _animalWork = new();
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
        _toolAnimator      = toolAnimator;
        _nav               = nav;
        _workAreaScanner   = workAreaScanner;
        _indoorScanner     = indoorScanner;
        _animalHandler     = animalHandler;
        _buildingNavigator = buildingNavigator;
        _chestResolver     = chestResolver;
        _depositPlanner    = depositPlanner;
        _mailDispatcher    = mailDispatcher;
        _stuck             = new StuckDetector(config.StuckInitialWaitMinutes);
    }

    private static int Manhattan(TileCoord a, TileCoord b) =>
        Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);

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

        BeginDeposit();
    }

    public void StartShift(Contract contract, int dayDeposit, int dayRate, IConfigSnapshot runtimeConfig)
    {
        if (_ctx is not null)
        {
            ModEntry.ModMonitor.Log("[Dayswork] StartShift called while a shift is already active — ignoring.", LogLevel.Warn);
            return;
        }

        _config = runtimeConfig;

        var farm     = Game1.getFarm();
        var snapshot = _toolReader.ReadSnapshot(Game1.player);

        var batches = BuildInitialBatches(contract, farm, snapshot);

        if (batches.Count == 0 ||
            batches.All(batch => batch.Kind == BatchKind.OutdoorFarm &&
                                 batch.TileWork.Count == 0 &&
                                 batch.AnimalWork.Count == 0 &&
                                 !batch.FeedBuilding))
        {
            // Empty zone (FR-PAY-06 / FD-Q6=C): the deposit is already paid (at hire for one-time, at
            // 6am for recurring), so refund it in full by mail next morning — no worker spawns.
            ModEntry.ModMonitor.Log("[Dayswork] No applicable work found for today's contract — refunding deposit by mail.", LogLevel.Info);
            if (dayDeposit > 0)
                _mailDispatcher.QueueSettlement(Array.Empty<ItemStack>(), new HashSet<OverflowReason>(), dayDeposit);
            return;
        }

        var spawnPos = new Vector2(FarmEntrance.X, FarmEntrance.Y) * 64f;
        _farmhand = new FarmhandNpc(spawnPos);
        farm.addCharacter(_farmhand);
        _currentLocation = farm;
        _toolAnimator.SetWorker(_farmhand);

        // Reset shift-level state.
        _stuck              = new StuckDetector(_config.StuckInitialWaitMinutes);
        _lastSampledGameTime = Game1.timeOfDay;
        _lastTilePos         = _farmhand.TilePoint;
        _playerWasSwinging   = false;
        _actionPending       = false;
        _waitingForDebrisBeforeDeposit = false;
        _exitWalkStarted     = false;
        _morningEntranceHoldTicks = MorningEntranceHoldTicks;
        _pendingBuildingEntry = false;
        _pendingBuildingExit = false;
        _pendingBuildingInterior = null;
        _currentFeedPlan = null;
        _hayInHand = 0;
        _animalWork.Clear();
        _currentAnimalWork = null;
        _pendingDebrisSweeps.Clear();

        _ctx = new ShiftContext(
            contractId:       contract.Id,
            zones:            contract.Zones,
            enabledTasks:     contract.EnabledTasks,
            taskDestinations: contract.TaskDestinations,
            depositAmount:    dayDeposit,
            hourlyRate:       dayRate,
            toolSnapshot:     snapshot,
            workList:         Array.Empty<WorkItem>(),
            shiftStartTime:   Game1.timeOfDay,
            batches:          batches);

        BeginCurrentBatch();
    }

    private IReadOnlyList<WorkBatch> BuildInitialBatches(Contract contract, Farm farm, ToolSnapshot snapshot)
    {
        var normalizedZones = contract.Zones
            .Select(zone => zone.LocationName == "Farm"
                ? zone
                : zone with { LocationName = BuildingLocationResolver.NormalizeLocationName(farm, zone.LocationName) })
            .ToList();

        ModEntry.ModMonitor.Log(
            "[Dayswork][shift-plan] zones=" +
            string.Join("; ", contract.Zones.Zip(normalizedZones, (raw, normalized) =>
                $"{raw.LocationName}->{normalized.LocationName}")),
            LogLevel.Info);

        var skeletons = _shiftPlanBuilder.BuildBatchPlan(normalizedZones, contract.EnabledTasks, IsAnimalHouseLocation);
        var selectedAnimalHomes = skeletons
            .Where(batch => batch.Kind == BatchKind.AnimalBuilding)
            .Select(batch => batch.LocationName)
            .ToHashSet(StringComparer.Ordinal);

        var outdoorZones = normalizedZones
            .Where(zone => zone.LocationName == "Farm")
            .ToList();

        var batches = new List<WorkBatch>(skeletons.Count);
        foreach (var batch in skeletons)
        {
            if (batch.Kind != BatchKind.OutdoorFarm)
            {
                batches.Add(batch);
                continue;
            }

            var tileWork = outdoorZones.Count == 0
                ? Array.Empty<WorkItem>()
                : _workAreaScanner.ScanZones(farm, outdoorZones, contract.EnabledTasks, snapshot, FarmEntrance);
            var animalWork = BuildAnimalWork(farm, selectedAnimalHomes, contract.EnabledTasks);
            batches.Add(batch with { TileWork = tileWork, AnimalWork = animalWork });
        }

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
            if (enabledTasks.Contains(TaskKind.PetAnimals) && _animalHandler.ShouldPet(liveAnimal))
                work.Add(new AnimalWorkItem(location.Name, animalRef, TaskKind.PetAnimals));

            if (enabledTasks.Contains(TaskKind.CollectAnimalProducts) && _animalHandler.HasToolHarvestReady(liveAnimal))
                work.Add(new AnimalWorkItem(location.Name, animalRef, TaskKind.CollectAnimalProducts));
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
        _currentAnimalWork = null;
        _pendingBuildingEntry = false;
        _pendingBuildingExit = false;
        _pendingBuildingInterior = null;
        _currentFeedPlan = null;
        _hayInHand = 0;

        if (_ctx.CurrentBatchIndex >= _ctx.Batches.Count)
        {
            _ctx.ShiftEndTime ??= Game1.timeOfDay;
            EnsureWorkingIntent(new IntentMoveToTile(FarmEntrance));
            BeginDeposit();
            return;
        }

        var batch = _ctx.Batches[_ctx.CurrentBatchIndex];
        if (batch.Kind != BatchKind.OutdoorFarm)
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
        batch = RefreshOutdoorAnimalWork(batch, _currentLocation);
        QueueBatchWork(batch, _currentLocation);
        StartNextAnimalOrTileOrAdvance();
    }

    private WorkBatch RefreshOutdoorAnimalWork(WorkBatch batch, GameLocation farm)
    {
        if (_ctx is null || batch.Kind != BatchKind.OutdoorFarm)
            return batch;

        var selectedAnimalHomes = _ctx.Batches
            .Where(candidate => candidate.Kind == BatchKind.AnimalBuilding)
            .Select(candidate => candidate.LocationName)
            .ToHashSet(StringComparer.Ordinal);

        var refreshedAnimalWork = BuildAnimalWork(farm, selectedAnimalHomes, _ctx.EnabledTasks);
        ModEntry.ModMonitor.Log(
            $"[Dayswork][outdoor-animals] refreshed homes={selectedAnimalHomes.Count} animalWork={refreshedAnimalWork.Count}.",
            refreshedAnimalWork.Count == 0 ? LogLevel.Debug : LogLevel.Info);
        return batch with { AnimalWork = refreshedAnimalWork };
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

        var tileWork = _indoorScanner.ScanInterior(interior, _ctx.EnabledTasks, _ctx.ToolSnapshot);
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
        var animalWork = BuildAnimalWork(interior, selectedHome, _ctx.EnabledTasks);
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

        _currentLocation = location;
    }

    private void StartNextAnimalOrTileOrAdvance()
    {
        if (_ctx is null || _farmhand is null)
            return;

        _stuck.Reset();
        _actionPending = false;

        if (_animalWork.Count > 0)
        {
            StartNextAnimalWork();
            return;
        }

        if (_ctx.WorkList.Count > 0)
        {
            StartNextTileWork(_ctx.WorkList.Dequeue());
            return;
        }

        CompleteCurrentBatch();
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
        if (batch.Kind != BatchKind.OutdoorFarm)
        {
            var interior = _currentLocation;
            if (interior is not null && interior != Game1.getFarm())
            {
                var exitTile = _buildingNavigator.ResolveInteriorExitApproachTile(interior);
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
        if (batch.Kind != BatchKind.OutdoorFarm)
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
    // no remaining tasks run headlessly, but collected/undelivered items and any refund are settled before
    // contracts persist and the day rolls over.
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

        // Refund is mailed next morning (DEV-U15-04), combined with any overflow into one settlement letter.
        var refund = _ctx.ComputeRefund();
        SettleShiftMail(refund);

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
            ModEntry.ModMonitor.Log("[Dayswork] 8pm cap reached — transitioning to deposit.", LogLevel.Trace);
            _ctx.ShiftEndTime = e.NewTime;
            BeginDeposit();
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
            ModEntry.ModMonitor.Log(
                $"[Dayswork][animal] skipping unreachable animal {_currentAnimalWork.Animal.DisplayName} ({_currentAnimalWork.Task}).",
                LogLevel.Debug);
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

        // Find the next reachable task tile.
        TileCoord? recoveryTile = null;
        foreach (var item in _ctx.WorkList)
        {
            if (IsTileReachable(item.NavTile, location))
            {
                recoveryTile = item.NavTile;
                break;
            }
        }

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
        // The next work item drives the real nav; re-queue with the movement driver.
        if (_ctx.WorkList.Count > 0)
        {
            StartNextTileWork(_ctx.WorkList.Dequeue());
        }
        else
        {
            // Nothing left to do — deposit.
            _ctx.ShiftEndTime = Game1.timeOfDay;
            _ctx.StateMachine.Transition(ShiftPhase.Working, new IntentMoveToTile(ShippingBinTile));
            BeginDeposit();
        }
    }

    private void HandleTeleportHome(Farm farm)
    {
        // Step 3: reposition home and end shift via normal Depositing path (SAFE-U13-01).
        _farmhand!.Position = new Vector2(FarmEntrance.X, FarmEntrance.Y) * 64f;
        _farmhand.currentLocation = farm;
        _currentLocation = farm;
        _nav.Clear();
        _ctx!.ShiftEndTime = Game1.timeOfDay;
        // Recovering → Depositing (valid successor per BR-SM-01).
        BeginDeposit();
    }

    // ── Hit-reaction watcher (Pattern H / BR-INVULN-01/02) ───────────────────

    private void CheckHitReaction()
    {
        if (_farmhand is null || _ctx is null) return;

        bool isSwinging = Game1.player.UsingTool && Game1.player.CurrentTool is MeleeWeapon;

        if (isSwinging && !_playerWasSwinging)
        {
            // Fresh swing — check if player is within range.
            float dist = Math.Abs(_farmhand.TilePoint.X - Game1.player.TilePoint.X)
                       + Math.Abs(_farmhand.TilePoint.Y - Game1.player.TilePoint.Y);
            if (dist <= HitRangeTiles)
                _farmhand.doEmote(EmoteExclamation); // one emote per swing — debounced by flag
        }

        _playerWasSwinging = isSwinging;
    }

    // ── Movement handler ─────────────────────────────────────────────────────

    private void HandleMovement(GameLocation location)
    {
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
                ModEntry.ModMonitor.Log(
                    $"[Dayswork][animal] navigation failed for {_currentAnimalWork.Animal.DisplayName} ({_currentAnimalWork.Task}); skipping.",
                    LogLevel.Debug);
                _currentAnimalWork = null;
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
                _ctx!.StateMachine.SetIntent(_currentAnimalWork.Task == TaskKind.PetAnimals
                    ? new IntentPetAnimal(_currentAnimalWork.Animal)
                    : new IntentCollectFromAnimal(_currentAnimalWork.Animal));
                return;
            }

            ModEntry.ModMonitor.Log(
                $"[Dayswork][nav] arrived task={_pendingTask} nav=({_pendingNavTile.X},{_pendingNavTile.Y}) task=({_pendingTaskTile.X},{_pendingTaskTile.Y}) worker=({_farmhand!.TilePoint.X},{_farmhand.TilePoint.Y}) fallback={_nav.UsedDirectFallback}.",
                LogLevel.Debug);
            _ctx!.StateMachine.SetIntent(new IntentPerformTaskAt(_pendingTaskTile, _pendingTask));
            _actionPending = false;
        }
    }

    private void HandleTaskAction(IntentPerformTaskAt intent, GameLocation location)
    {
        if (!_actionPending)
        {
            _toolAnimator.StopSwing();
            _toolAnimator.PlaySwing(intent.Task, FacingToward(_farmhand!.TilePoint, intent.Tile, _farmhand.FacingDirection));
            ModEntry.ModMonitor.Log(
                $"[Dayswork][action] invoke task={intent.Task} taskTile=({intent.Tile.X},{intent.Tile.Y}) worker=({_farmhand.TilePoint.X},{_farmhand.TilePoint.Y}).",
                LogLevel.Debug);
            InvokeTaskAction(intent.Tile, intent.Task, location);
            _actionPending = true;
            return;
        }

        if (_toolAnimator.IsSwinging)
            return;

        if (IsTaskComplete(intent.Tile, intent.Task, location))
        {
            ModEntry.ModMonitor.Log(
                $"[Dayswork][action] complete task={intent.Task} taskTile=({intent.Tile.X},{intent.Tile.Y}).",
                LogLevel.Debug);
            _actionPending = false;
            AdvanceWorkList(location);
            return;
        }

        _actionPending = false;
    }

    private void HandlePetAnimal(IntentPetAnimal intent)
    {
        var location = _currentLocation ?? Game1.getFarm();
        var animal = _animalHandler.FindLiveAnimal(location, intent.Animal);
        if (animal is not null)
            _animalHandler.Pet(animal);

        _currentAnimalWork = null;
        StartNextAnimalOrTileOrAdvance();
    }

    private void HandleCollectFromAnimal(IntentCollectFromAnimal intent)
    {
        var location = _currentLocation ?? Game1.getFarm();
        var animal = _animalHandler.FindLiveAnimal(location, intent.Animal);
        if (animal is null)
        {
            _currentAnimalWork = null;
            StartNextAnimalOrTileOrAdvance();
            return;
        }

        if (!_actionPending)
        {
            _toolAnimator.StopSwing();
            _toolAnimator.PlaySwing(TaskKind.CollectAnimalProducts,
                FacingToward(_farmhand!.TilePoint, _animalHandler.CurrentTile(animal), _farmhand.FacingDirection));
            PlayAnimalCollectSound(location, animal);
            _actionPending = true;
            return;
        }

        if (_toolAnimator.IsSwinging)
            return;

        _animalHandler.TryCollect(animal, _ctx!.Buffer);
        _actionPending = false;
        _currentAnimalWork = null;
        StartNextAnimalOrTileOrAdvance();
    }

    private static void PlayAnimalCollectSound(GameLocation location, FarmAnimal animal)
    {
        var sound = AnimalTaskHandler.IsShearProduce(animal)
            ? "Shears"
            : AnimalTaskHandler.IsMilkProduce(animal)
                ? "Milking"
                : "dwop";

        location.playSound(sound);
    }

    // ── Deposit / exit handlers ───────────────────────────────────────────────

    private void HandleDeposit(Farm farm)
    {
        if (!_nav.NavigationFailed && !_nav.HasArrived)
            return;

        // Execute the trip we just walked to (chest liveness resolved here, on arrival).
        if (_currentTrip is not null)
        {
            ExecuteTrip(_currentTrip, _currentLocation ?? farm);
            CompleteDepositTripLocation(_currentTrip);
        }
        _currentTrip = null;

        // Walk the next trip, or exit once the queue is empty (Pattern N).
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

    private void ExecuteTrip(DepositTrip trip, GameLocation location)
    {
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
                    LogLevel.Debug);
                return;
            }

            foreach (var stack in trip.Items)
                DepositIntoChest(chest, stack);
        }
        else
        {
            // Shipping bin — infinite capacity, never overflows (FR-OUT-06).
            var bin = Game1.getFarm().getShippingBin(Game1.player);
            foreach (var stack in trip.Items)
            {
                var item = ItemRegistry.Create(stack.QualifiedItemId, stack.Quantity);
                if (item is not null)
                    bin.Add(item);
            }
        }
    }

    private void DepositIntoChest(Chest chest, ItemStack stack)
    {
        var item = ItemRegistry.Create(stack.QualifiedItemId, stack.Quantity);
        if (item is null)
            return;

        // addItem returns the remainder that did not fit (null if all fit).
        var leftover = chest.addItem(item);
        if (leftover is not null && leftover.Stack > 0)
        {
            // Chest full (FR-OUT-02): mail the remainder (ChestFull).
            _ctx!.Overflow.Add(new OverflowItem(
                new ItemStack(stack.QualifiedItemId, leftover.Stack), OverflowReason.ChestFull));
            ModEntry.ModMonitor.Log(
                $"[Dayswork][deposit] chest full; {leftover.Stack}x {stack.QualifiedItemId} → mail.",
                LogLevel.Debug);
        }
    }

    private static ShiftIntent ToDepositIntent(DepositTrip trip) =>
        trip.Destination is ChestDestination cd
            ? new IntentDepositAtChest(cd.Ref)
            : new IntentDepositInShippingBin();

    private void BeginExit(Farm farm)
    {
        _ctx!.StateMachine.Transition(ShiftPhase.Exiting, new IntentExitFarm());
        _exitWalkStarted = false;
        _nav.StartNavigation(FarmEntrance, farm, _farmhand!);
    }

    private void HandleExit(Farm farm)
    {
        if (!_nav.NavigationFailed && !_nav.HasArrived)
            return;

        if (!_exitWalkStarted)
        {
            _exitWalkStarted = true;
            _toolAnimator.StopSwing();
            ModEntry.ModMonitor.Log("[Dayswork][exit] worker leaving through farm entrance.", LogLevel.Debug);
            _nav.StartForcedPixelRoute(farm, _farmhand!, FarmExitPastEntrancePixel);
            return;
        }

        var refund = _ctx!.ComputeRefund();

        ModEntry.ModMonitor.Log(
            $"[Dayswork] Shift complete. Hours: {((_ctx.ShiftEndTime ?? Game1.timeOfDay) - _ctx.ShiftStartTime) / 60}. Refund (mailed): {refund}g.",
            LogLevel.Info);

        // One settlement letter next morning: any overflow items + the refund gold (Pattern U / DEV-U15-04).
        SettleShiftMail(refund);

        ClearWorker();
        _ctx.StateMachine.Transition(ShiftPhase.Done);
        _ctx = null;
    }

    // ── Work list helpers ─────────────────────────────────────────────────────

    private void AdvanceWorkList(GameLocation location)
    {
        _stuck.Reset(); // any advance = progress signal

        if (_ctx!.WorkList.Count == 0)
        {
            StartNextAnimalOrTileOrAdvance();
            return;
        }

        StartNextTileWork(_ctx.WorkList.Dequeue());
    }

    private void BeginDeposit()
    {
        var farm = Game1.getFarm();
        ReturnWorkerToFarmForDeposit();
        // Valid from Working, Stuck, Recovering (all have Depositing as a successor).
        _morningEntranceHoldTicks = 0;
        if (_pendingDebrisSweeps.Count > 0)
        {
            _waitingForDebrisBeforeDeposit = true;
            _actionPending = true;
            _toolAnimator.StopSwing();
            ModEntry.ModMonitor.Log(
                $"[Dayswork][debris] waiting for {_pendingDebrisSweeps.Count} pending debris sweep(s) before deposit.",
                LogLevel.Debug);
            return;
        }

        FlushPendingDebrisSweeps();

        // Plan the deposit run from the task-tagged buffer (Pattern M).
        var workerTile = _farmhand is not null
            ? new TileCoord(_farmhand.TilePoint.X, _farmhand.TilePoint.Y)
            : FarmEntrance;
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
        if (_depositTrips.Count == 0)
        {
            _ctx.StateMachine.Transition(ShiftPhase.Depositing, new IntentDepositInShippingBin());
            BeginExit(farm);
            return;
        }

        var first = _depositTrips.Dequeue();
        _currentTrip = first;
        _ctx.StateMachine.Transition(ShiftPhase.Depositing, ToDepositIntent(first));
        StartDepositTrip(first);
    }

    private void StartDepositTrip(DepositTrip trip)
    {
        if (_farmhand is null)
            return;

        var farm = Game1.getFarm();
        if (trip.Destination is ChestDestination { Ref.LocationName: not "Farm" } chestDest)
        {
            if (_buildingNavigator.TryResolveDoorTile(chestDest.Ref.LocationName, out var _, out var interior))
            {
                var entryTile = _buildingNavigator.ResolveInteriorEntryTile(interior);
                _buildingNavigator.Enter(_farmhand, interior, entryTile);
                _currentLocation = interior;
                _nav.StartNavigation(trip.Tile, interior, _farmhand);
                return;
            }

            foreach (var stack in trip.Items)
                _ctx!.Overflow.Add(new OverflowItem(stack, OverflowReason.ChestMissing));
            _currentTrip = null;
            return;
        }

        _currentLocation = farm;
        _nav.StartNavigation(trip.Tile, farm, _farmhand);
    }

    private void CompleteDepositTripLocation(DepositTrip trip)
    {
        if (_farmhand is null ||
            trip.Destination is not ChestDestination { Ref.LocationName: not "Farm" } chestDest)
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

        var batch = _ctx is not null && _ctx.CurrentBatchIndex < _ctx.Batches.Count
            ? _ctx.Batches[_ctx.CurrentBatchIndex]
            : null;

        var exitTile = batch is not null &&
                       _buildingNavigator.TryResolveDoorTile(batch.LocationName, out var outdoorDoor, out _)
            ? outdoorDoor
            : FarmEntrance;

        _buildingNavigator.ExitToFarm(_farmhand, exitTile);
        _currentLocation = farm;
    }

    // ── Overflow / mail helpers (Pattern O) ───────────────────────────────────

    // One settlement letter per shift: overflow items (if any) + refund gold (if > 0). Sends nothing
    // when there is neither (Pattern U / BR-REF-03).
    private void SettleShiftMail(int refundGold)
    {
        if (_ctx is null) return;

        IReadOnlyList<ItemStack> items = _ctx.Overflow.Count > 0
            ? ConsolidateOverflow(_ctx.Overflow)
            : Array.Empty<ItemStack>();
        var reasons = _ctx.Overflow.Select(o => o.Reason).ToHashSet();

        _mailDispatcher.QueueSettlement(items, reasons, refundGold);
        _ctx.Overflow.Clear();
    }

    private static IReadOnlyList<ItemStack> ConsolidateOverflow(IEnumerable<OverflowItem> overflow)
    {
        var totals = new Dictionary<string, int>();
        foreach (var o in overflow)
            totals[o.Stack.QualifiedItemId] =
                totals.TryGetValue(o.Stack.QualifiedItemId, out var e) ? e + o.Stack.Quantity : o.Stack.Quantity;
        return totals.Select(kv => new ItemStack(kv.Key, kv.Value)).ToList();
    }

    // Moves everything still undelivered (buffer + in-flight trip + queued trips) into the overflow
    // set as NotDelivered, so a save mid-cleanup loses nothing (FD-Q5=A / BR-INT-01).
    private void AppendUndeliveredToOverflow()
    {
        if (_ctx is null) return;

        foreach (var b in _ctx.Buffer.TakeAll())
            _ctx.Overflow.Add(new OverflowItem(
                new ItemStack(b.QualifiedItemId, b.Quantity), OverflowReason.NotDelivered));

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
            (_farmhand.currentLocation ?? _currentLocation ?? Game1.getFarm()).characters.Remove(_farmhand);
            Game1.getFarm().characters.Remove(_farmhand);
        }

        _toolAnimator.SetWorker(null);
        _farmhand = null;
        _currentLocation = null;
        _morningEntranceHoldTicks = 0;
        _waitingForDebrisBeforeDeposit = false;
        _exitWalkStarted = false;
        _pendingBuildingEntry = false;
        _pendingBuildingExit = false;
        _pendingBuildingInterior = null;
        _currentFeedPlan = null;
        _hayInHand = 0;
        _animalWork.Clear();
        _currentAnimalWork = null;
        _pendingDebrisSweeps.Clear();
        _depositTrips.Clear();
        _currentTrip = null;
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

    private void InvokeTaskAction(TileCoord tile, TaskKind task, GameLocation location)
    {
        switch (task)
        {
            case TaskKind.WaterCrops:   InvokeWater(tile, location);        break;
            case TaskKind.HarvestCrops: InvokeHarvest(tile, location);      break;
            case TaskKind.CollectFruit: InvokeCollectFruit(tile, location);  break;
            case TaskKind.CollectAnimalProducts: InvokeCollectAnimalProduct(tile, location); break;
            case TaskKind.FeedAnimals: InvokeFeedAnimal(tile, location); break;
            case TaskKind.ClearWeeds:   InvokeClearWeed(tile, location);    break;
            case TaskKind.ClearGrass:   InvokeClearGrass(tile, location);   break;
            case TaskKind.ClearRocks:   InvokeClearRock(tile, location);    break;
            case TaskKind.CutTrees:     InvokeCutTree(tile, location);      break;
        }
    }

    private static void InvokeWater(TileCoord tile, GameLocation loc)
    {
        if (loc.terrainFeatures.TryGetValue(new Vector2(tile.X, tile.Y), out var tf) && tf is HoeDirt dirt)
            dirt.state.Value = HoeDirt.watered;
    }

    private void InvokeHarvest(TileCoord tile, GameLocation loc)
    {
        var tileVec = new Vector2(tile.X, tile.Y);
        if (!loc.terrainFeatures.TryGetValue(tileVec, out var tf) || tf is not HoeDirt dirt || dirt.crop is null)
            return;

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
                _ctx!.Buffer.Add(item.QualifiedItemId, item.Stack, TaskKind.HarvestCrops);
                Game1.player.removeItemFromInventory(item);
            }
            else if (item.Stack > oldStack)
            {
                // Existing slot grew (stacked onto items the player already had).
                var gain = item.Stack - oldStack;
                _ctx!.Buffer.Add(item.QualifiedItemId, gain, TaskKind.HarvestCrops);
                item.Stack -= gain;
                if (item.Stack <= 0)
                    Game1.player.removeItemFromInventory(item);
            }
        }

        // crop.harvest() does not clean up dirt.crop — vanilla expects the caller to do it.
        // For non-regrowable crops call HoeDirt.destroyCrop (the authoritative cleanup path).
        // Regrowable crops stay on the dirt and start regrowing; HoeDirt.readyForHarvest()
        // will return false until they're ready again, so IsTaskComplete terminates naturally.
        if (dirt.crop is not null && !dirt.crop.RegrowsAfterHarvest())
            dirt.destroyCrop(false);

        // Also collect any debris-spawned items (in case harvest used the debris path).
        CollectNewDebrisAtTile(before, loc, _pendingTask, tileVec);
    }

    private void InvokeCollectFruit(TileCoord tile, GameLocation loc)
    {
        var tileVec = new Vector2(tile.X, tile.Y);
        if (!loc.terrainFeatures.TryGetValue(tileVec, out var tf) || tf is not FruitTree tree) return;
        var before = new HashSet<Debris>(loc.debris);
        tree.shake(tileVec, false);
        CollectNewDebrisAtTile(before, loc, _pendingTask, tileVec);
    }

    private void InvokeCollectAnimalProduct(TileCoord tile, GameLocation loc)
    {
        var tileVec = new Vector2(tile.X, tile.Y);
        if (!loc.objects.TryGetValue(tileVec, out var obj) ||
            !WorkAreaScanner.IsAnimalProductForageObject(obj))
            return;

        _ctx!.Buffer.Add(obj.QualifiedItemId, Math.Max(1, obj.Stack), TaskKind.CollectAnimalProducts);
        loc.removeObject(tileVec, false);
    }

    private void InvokeFeedAnimal(TileCoord tile, GameLocation loc)
    {
        if (_currentFeedPlan is null)
            return;

        if (tile == _currentFeedPlan.HopperTile && _hayInHand <= 0)
        {
            if (_animalHandler.TakeHay(loc, _currentFeedPlan.HayToTake))
                _hayInHand = _currentFeedPlan.HayToTake;
            return;
        }

        if (_hayInHand <= 0)
            return;

        if (_animalHandler.PlaceHay(loc, tile))
            _hayInHand--;
    }

    private void InvokeClearWeed(TileCoord tile, GameLocation loc)
    {
        var tileVec = new Vector2(tile.X, tile.Y);
        if (!loc.objects.TryGetValue(tileVec, out var obj) || !obj.IsWeeds()) return;
        var before = new HashSet<Debris>(loc.debris);
        var scythe = new MeleeWeapon("66") { lastUser = Game1.player };
        obj.performToolAction(scythe);
        if (loc.objects.ContainsKey(tileVec))
            loc.removeObject(tileVec, false);
        CollectNewDebrisAtTile(before, loc, _pendingTask, tileVec);
    }

    private void InvokeClearGrass(TileCoord tile, GameLocation loc)
    {
        var tileVec = new Vector2(tile.X, tile.Y);
        if (!loc.terrainFeatures.TryGetValue(tileVec, out var tf) || tf is not Grass grass) return;
        var scythe = new MeleeWeapon("66") { lastUser = Game1.player };
        grass.performToolAction(scythe, 0, tileVec);
        if (loc.terrainFeatures.ContainsKey(tileVec))
            loc.terrainFeatures.Remove(tileVec);
    }

    private void InvokeClearRock(TileCoord tile, GameLocation loc)
    {
        var tileVec = new Vector2(tile.X, tile.Y);
        var pickaxe = new Pickaxe { UpgradeLevel = (int)_ctx!.ToolSnapshot.PickaxeLevel, lastUser = Game1.player };

        if (ObjectTargetClassifier.FindResourceClumpAt(tileVec, loc) is { } clump)
        {
            // One pickaxe hit per action cycle. Pass damage=1 (standard per-swing value).
            // performToolAction returns true only when health reaches 0.
            var beforeHit = new HashSet<Debris>(loc.debris);
            var destroyed = clump.performToolAction(pickaxe, 1, clump.Tile);
            CollectNewDebrisAtTile(beforeHit, loc, _pendingTask, clump.Tile);

            if (destroyed)
            {
                // destroy() spawns the actual loot drops; collect them immediately after.
                var beforeDestroy = new HashSet<Debris>(loc.debris);
                clump.destroy(pickaxe, loc, clump.Tile);
                loc.resourceClumps.Remove(clump); // ensure removed even if destroy didn't
                CollectNewDebrisAtTile(beforeDestroy, loc, _pendingTask, clump.Tile);
            }
            // If not destroyed, IsTaskComplete still finds the clump → action loop re-fires next swing.
            return;
        }

        if (!loc.objects.TryGetValue(tileVec, out var obj)) return;
        if (ObjectTargetClassifier.ClassifyPick(tileVec, loc) is null) return;

        var before  = new HashSet<Debris>(loc.debris);
        var actionRemoved = obj.performToolAction(pickaxe);
        if (loc.objects.ContainsKey(tileVec))
            loc.removeObject(tileVec, false);
        var removed = !loc.objects.ContainsKey(tileVec);
        ModEntry.ModMonitor.Log(
            $"[Dayswork][action] clear rock at ({tile.X},{tile.Y}) performToolAction={actionRemoved} removed={removed}.",
            LogLevel.Debug);
        var collectedDebris = CollectNewDebrisAtTile(before, loc, _pendingTask, tileVec);
        if (!collectedDebris && removed && TryGetRemovedStandardStoneDrop(obj, out var itemId, out var stack))
        {
            _ctx.Buffer.Add(itemId, stack, _pendingTask);
            ModEntry.ModMonitor.Log(
                $"[Dayswork][debris] collected {stack}x {itemId} from removed standard stone object task={_pendingTask}.",
                LogLevel.Debug);
        }
    }

    private void InvokeCutTree(TileCoord tile, GameLocation loc)
    {
        var tileVec = new Vector2(tile.X, tile.Y);
        var axe = new Axe { UpgradeLevel = (int)_ctx!.ToolSnapshot.AxeLevel, lastUser = Game1.player };

        if (loc.terrainFeatures.TryGetValue(tileVec, out var tf) && tf is Tree tree)
        {
            bool wasStump   = tree.stump.Value;
            var  before     = new HashSet<Debris>(loc.debris);
            var  removeTree = tree.performToolAction(axe, 0, tileVec);
            if (removeTree && loc.terrainFeatures.ContainsKey(tileVec))
                loc.terrainFeatures.Remove(tileVec);
            ModEntry.ModMonitor.Log(
                $"[Dayswork][action] cut tree at ({tile.X},{tile.Y}) remove={removeTree} health={tree.health.Value:0.##} stump={tree.stump.Value}.",
                LogLevel.Debug);
            CollectNewDebrisAtTile(before, loc, _pendingTask, tileVec);
            if (!wasStump && !removeTree)
                QueueDelayedDebrisSweep(loc, tileVec, before, _pendingTask);
            return;
        }

        if (ObjectTargetClassifier.FindResourceClumpAt(tileVec, loc) is { } clump)
        {
            var before = new HashSet<Debris>(loc.debris);
            clump.performToolAction(axe, 0, clump.Tile);
            loc.resourceClumps.Remove(clump);
            CollectNewDebrisAtTile(before, loc, _pendingTask, clump.Tile);
            return;
        }

        if (loc.objects.TryGetValue(tileVec, out var obj) && obj.Name == "Twig")
        {
            var before = new HashSet<Debris>(loc.debris);
            obj.performToolAction(axe);
            if (loc.objects.ContainsKey(tileVec))
                loc.removeObject(tileVec, false);
            CollectNewDebrisAtTile(before, loc, _pendingTask, tileVec);
        }
    }

    private bool CollectNewDebrisAtTile(
        HashSet<Debris> before,
        GameLocation loc,
        TaskKind sourceTask,
        Vector2 tileVec) =>
        CollectNewDebris(
            before,
            loc,
            sourceTask,
            new Vector2(tileVec.X * 64f + 32f, tileVec.Y * 64f + 32f),
            ImmediateDebrisSweepRadiusTiles);

    private bool CollectNewDebris(
        HashSet<Debris> before,
        GameLocation loc,
        TaskKind sourceTask,
        Vector2? origin = null,
        int radiusTiles = int.MaxValue)
    {
        bool collected = false;
        foreach (var d in loc.debris.ToList())
        {
            if (before.Contains(d) ||
                (origin.HasValue && !IsDebrisNear(d, origin.Value, radiusTiles)) ||
                !TryGetDebrisItem(d, out var itemId, out var stack))
                continue;

            _ctx!.Buffer.Add(itemId, stack, sourceTask);
            ModEntry.ModMonitor.Log(
                $"[Dayswork][debris] collected {stack}x {itemId} from game debris task={sourceTask} chunks={d.Chunks.Count} debrisType={d.debrisType.Value} chunkType={d.chunkType.Value}.",
                LogLevel.Debug);
            loc.debris.Remove(d);
            collected = true;
        }
        return collected;
    }

    private static bool TryGetDebrisItem(Debris debris, out string itemId, out int stack)
    {
        if (debris.item is not null)
        {
            itemId = debris.item.QualifiedItemId;
            stack  = Math.Max(1, debris.item.Stack);
            return true;
        }

        var debrisItemId = debris.itemId.Value;
        if (!string.IsNullOrWhiteSpace(debrisItemId))
        {
            itemId = debrisItemId;
            stack = debris.debrisType.Value == Debris.DebrisType.RESOURCE
                ? Math.Max(1, debris.Chunks.Count)
                : 1;
            return true;
        }

        itemId = "";
        stack = 0;
        return false;
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

    private void QueueDelayedDebrisSweep(GameLocation loc, Vector2 tileVec, HashSet<Debris> baseline, TaskKind sourceTask)
    {
        var origin = new Vector2(tileVec.X * 64f + 32f, tileVec.Y * 64f + 32f);
        _pendingDebrisSweeps.Add(new PendingDebrisSweep(
            loc,
            origin,
            baseline,
            DelayedTreeDebrisSweepTicks,
            DelayedTreeDebrisSweepRadiusTiles,
            sourceTask));
    }

    private void ProcessPendingDebrisSweeps()
    {
        for (var i = _pendingDebrisSweeps.Count - 1; i >= 0; i--)
        {
            var sweep = _pendingDebrisSweeps[i];
            CollectNewDebris(sweep.Baseline, sweep.Location, sweep.SourceTask, sweep.Origin, sweep.RadiusTiles);
            sweep.TicksRemaining--;
            if (sweep.TicksRemaining <= 0)
                _pendingDebrisSweeps.RemoveAt(i);
        }
    }

    private void FlushPendingDebrisSweeps()
    {
        foreach (var sweep in _pendingDebrisSweeps)
            CollectNewDebris(sweep.Baseline, sweep.Location, sweep.SourceTask, sweep.Origin, sweep.RadiusTiles);

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
            TaskKind sourceTask)
        {
            Location = location;
            Origin = origin;
            Baseline = baseline;
            TicksRemaining = ticksRemaining;
            RadiusTiles = radiusTiles;
            SourceTask = sourceTask;
        }

        public GameLocation Location { get; }
        public Vector2 Origin { get; }
        public HashSet<Debris> Baseline { get; }
        public int TicksRemaining { get; set; }
        public int RadiusTiles { get; }
        public TaskKind SourceTask { get; }
    }
}
