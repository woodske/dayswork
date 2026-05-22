using Dayswork.Core.Capabilities;
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
    private readonly ICapabilityEvaluator _capability      = new CapabilityEvaluator();
    private readonly ITaskPriorityOrderer _priorityOrderer = new TaskPriorityOrderer();
    private readonly IConfigSnapshot      _config;
    private readonly WorkerMovementDriver _nav = new();
    private readonly ChestResolver        _chestResolver;
    private readonly IDepositPlanner      _depositPlanner;
    private readonly IMailDispatcher      _mailDispatcher;

    private ShiftContext? _ctx;
    private FarmhandNpc?  _farmhand;
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
        ChestResolver chestResolver,
        IDepositPlanner depositPlanner,
        IMailDispatcher mailDispatcher)
    {
        _toolReader     = toolReader;
        _config         = config;
        _toolAnimator   = toolAnimator;
        _chestResolver  = chestResolver;
        _depositPlanner = depositPlanner;
        _mailDispatcher = mailDispatcher;
        _stuck          = new StuckDetector(config.StuckInitialWaitMinutes);
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

    public void StartShift(Contract contract, int dayDeposit, int dayRate)
    {
        if (_ctx is not null)
        {
            ModEntry.ModMonitor.Log("[Dayswork] StartShift called while a shift is already active — ignoring.", LogLevel.Warn);
            return;
        }

        var farm     = Game1.getFarm();
        var snapshot = _toolReader.ReadSnapshot(Game1.player);

        // Build work list before creating ShiftContext so we can pass it in.
        var workList = BuildWorkList(contract, farm, snapshot);

        if (workList.Count == 0)
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
        _pendingDebrisSweeps.Clear();

        var firstItem = workList[0];
        var remaining = workList.Skip(1);

        _ctx = new ShiftContext(
            contractId:       contract.Id,
            zones:            contract.Zones,
            enabledTasks:     contract.EnabledTasks,
            taskDestinations: contract.TaskDestinations,
            depositAmount:    dayDeposit,
            hourlyRate:       dayRate,
            toolSnapshot:     snapshot,
            workList:         remaining,
            shiftStartTime:   Game1.timeOfDay);

        _pendingTask     = firstItem.Task;
        _pendingNavTile  = firstItem.NavTile;
        _pendingTaskTile = firstItem.TaskTile;
        _toolAnimator.OnTaskChanged(firstItem.Task, firstItem.Task);
        _ctx.StateMachine.Transition(ShiftPhase.Working, new IntentMoveToTile(firstItem.NavTile));
        _nav.StartNavigation(firstItem.NavTile, farm, _farmhand);
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
        var phase = _ctx.StateMachine.Phase;

        // Progress sampling + stuck detection (Pattern D).
        // Only meaningful while actively working.
        if (phase == ShiftPhase.Working)
        {
            SampleProgress(farm);
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
                HandleMovement(farm);
                break;
            case IntentPerformTaskAt intent:
                HandleTaskAction(intent, farm);
                break;
            case IntentPlayEmote intent:
                HandlePlayEmote(intent, farm);
                break;
            case IntentTeleportToTile intent:
                HandleTeleportToTile(intent, farm);
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

    private void SampleProgress(Farm farm)
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
            BeginStuckEscalation(farm);
    }

    // ── Stuck escalation (Patterns D / E) ────────────────────────────────────

    /// <summary>
    /// Step 1: transition Working → Stuck with a "?" emote intent.
    /// HandlePlayEmote (called next tick) drives step 2 or 3 via QueueStuckTeleport.
    /// </summary>
    private void BeginStuckEscalation(Farm _)
    {
        _ctx!.StateMachine.Transition(ShiftPhase.Stuck, new IntentPlayEmote(EmoteQuestion));
    }

    private void HandlePlayEmote(IntentPlayEmote intent, Farm farm)
    {
        _farmhand!.doEmote(intent.EmoteId);
        QueueStuckTeleport(farm);
    }

    /// <summary>
    /// Decides step 2 vs step 3 of escalation and transitions Stuck → Recovering.
    /// Step 2: teleport to next reachable work tile (RecoveryAttempts == 0 and tile found).
    /// Step 3: teleport home and end shift early (RecoveryAttempts >= 1 or no reachable tile).
    /// </summary>
    private void QueueStuckTeleport(Farm farm)
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
            if (IsTileReachable(item.NavTile, farm))
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

    private void HandleTeleportToTile(IntentTeleportToTile intent, Farm farm)
    {
        // Instant reposition to recovery tile, then resume working.
        _farmhand!.Position = new Vector2(intent.Destination.X, intent.Destination.Y) * 64f;
        _farmhand.currentLocation = farm;
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
            var next = _ctx.WorkList.Dequeue();
            var previousTask = _pendingTask;
            _pendingTask     = next.Task;
            _pendingNavTile  = next.NavTile;
            _pendingTaskTile = next.TaskTile;
            _toolAnimator.OnTaskChanged(previousTask, next.Task);
            _ctx.StateMachine.Transition(ShiftPhase.Working, new IntentMoveToTile(next.NavTile));
            _nav.StartNavigation(next.NavTile, farm, _farmhand);
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

    private void HandleMovement(Farm farm)
    {
        if (_nav.NavigationFailed)
        {
            ModEntry.ModMonitor.Log(
                $"[Dayswork][nav] failed task={_pendingTask} nav=({_pendingNavTile.X},{_pendingNavTile.Y}) task=({_pendingTaskTile.X},{_pendingTaskTile.Y}); skipping.",
                LogLevel.Warn);
            AdvanceWorkList(farm);
            return;
        }

        if (_nav.HasArrived)
        {
            ModEntry.ModMonitor.Log(
                $"[Dayswork][nav] arrived task={_pendingTask} nav=({_pendingNavTile.X},{_pendingNavTile.Y}) task=({_pendingTaskTile.X},{_pendingTaskTile.Y}) worker=({_farmhand!.TilePoint.X},{_farmhand.TilePoint.Y}) fallback={_nav.UsedDirectFallback}.",
                LogLevel.Debug);
            _ctx!.StateMachine.SetIntent(new IntentPerformTaskAt(_pendingTaskTile, _pendingTask));
            _actionPending = false;
        }
    }

    private void HandleTaskAction(IntentPerformTaskAt intent, Farm farm)
    {
        if (!_actionPending)
        {
            _toolAnimator.StopSwing();
            _toolAnimator.PlaySwing(intent.Task, FacingToward(_farmhand!.TilePoint, intent.Tile, _farmhand.FacingDirection));
            ModEntry.ModMonitor.Log(
                $"[Dayswork][action] invoke task={intent.Task} taskTile=({intent.Tile.X},{intent.Tile.Y}) worker=({_farmhand.TilePoint.X},{_farmhand.TilePoint.Y}).",
                LogLevel.Debug);
            InvokeTaskAction(intent.Tile, intent.Task, farm);
            _actionPending = true;
            return;
        }

        if (_toolAnimator.IsSwinging)
            return;

        if (IsTaskComplete(intent.Tile, intent.Task, farm))
        {
            ModEntry.ModMonitor.Log(
                $"[Dayswork][action] complete task={intent.Task} taskTile=({intent.Tile.X},{intent.Tile.Y}).",
                LogLevel.Debug);
            _actionPending = false;
            AdvanceWorkList(farm);
            return;
        }

        _actionPending = false;
    }

    // ── Deposit / exit handlers ───────────────────────────────────────────────

    private void HandleDeposit(Farm farm)
    {
        if (!_nav.NavigationFailed && !_nav.HasArrived)
            return;

        // Execute the trip we just walked to (chest liveness resolved here, on arrival).
        if (_currentTrip is not null)
            ExecuteTrip(_currentTrip, farm);
        _currentTrip = null;

        // Walk the next trip, or exit once the queue is empty (Pattern N).
        if (_depositTrips.Count > 0)
        {
            var next = _depositTrips.Dequeue();
            _currentTrip = next;
            _ctx!.StateMachine.SetIntent(ToDepositIntent(next));
            _nav.StartNavigation(next.Tile, farm, _farmhand!);
            return;
        }

        BeginExit(farm);
    }

    private void ExecuteTrip(DepositTrip trip, Farm farm)
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
            var bin = farm.getShippingBin(Game1.player);
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

    private void AdvanceWorkList(Farm farm)
    {
        _stuck.Reset(); // any advance = progress signal

        if (_ctx!.WorkList.Count == 0)
        {
            _ctx.ShiftEndTime = Game1.timeOfDay;
            BeginDeposit();
            return;
        }

        var next = _ctx.WorkList.Dequeue();
        var previousTask = _pendingTask;
        _pendingTask     = next.Task;
        _pendingNavTile  = next.NavTile;
        _pendingTaskTile = next.TaskTile;
        _toolAnimator.StopSwing();
        _toolAnimator.OnTaskChanged(previousTask, next.Task);
        _ctx.StateMachine.SetIntent(new IntentMoveToTile(next.NavTile));
        _nav.StartNavigation(next.NavTile, farm, _farmhand!);
    }

    private void BeginDeposit()
    {
        var farm = Game1.getFarm();
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
        _nav.StartNavigation(first.Tile, farm, _farmhand!);
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
            Game1.getFarm().characters.Remove(_farmhand);
        }

        _toolAnimator.SetWorker(null);
        _farmhand = null;
        _morningEntranceHoldTicks = 0;
        _waitingForDebrisBeforeDeposit = false;
        _exitWalkStarted = false;
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

    private static bool IsTileReachable(TileCoord tile, Farm farm) =>
        WorkerMovementDriver.IsTilePassableForWorker(new Point(tile.X, tile.Y), farm);

    // ── Task invocation (Invoke-and-Poll) ─────────────────────────────────────

    private void InvokeTaskAction(TileCoord tile, TaskKind task, Farm farm)
    {
        switch (task)
        {
            case TaskKind.WaterCrops:   InvokeWater(tile, farm);        break;
            case TaskKind.HarvestCrops: InvokeHarvest(tile, farm);      break;
            case TaskKind.CollectFruit: InvokeCollectFruit(tile, farm);  break;
            case TaskKind.ClearWeeds:   InvokeClearWeed(tile, farm);    break;
            case TaskKind.ClearGrass:   InvokeClearGrass(tile, farm);   break;
            case TaskKind.ClearRocks:   InvokeClearRock(tile, farm);    break;
            case TaskKind.CutTrees:     InvokeCutTree(tile, farm);      break;
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
        dirt.crop.harvest(tile.X, tile.Y, dirt, null);
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
            var beforeClump = new HashSet<Debris>(loc.debris);
            clump.performToolAction(pickaxe, 0, clump.Tile);
            loc.resourceClumps.Remove(clump);
            CollectNewDebrisAtTile(beforeClump, loc, _pendingTask, clump.Tile);
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
                tf2 is not HoeDirt hd || hd.crop is null,

            TaskKind.CollectFruit =>
                !loc.terrainFeatures.TryGetValue(tileVec, out var tf3) ||
                tf3 is not FruitTree ft || ft.fruit.Count == 0,

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

    // ── Work list construction (Patterns A + B) ───────────────────────────────

    /// <summary>
    /// Scans the contract zones, applies capability and skip rules, then greedily routes
    /// to the nearest next outdoor task. Animal/building work is still deferred.
    /// </summary>
    private List<WorkItem> BuildWorkList(
        Contract contract,
        Farm farm,
        ToolSnapshot snapshot)
    {
        var enabled = contract.EnabledTasks;
        // A tile out of the player's tool tier is silently skipped (DEV-U15-03 keeps the tier gate but
        // drops the tool-missing warning that U-13/U-14 sent).
        var seenWorkItems = new HashSet<(TaskKind Task, TileCoord Tile)>();

        var rawItems = new List<(WorkItem item, TaskKind task)>();
        var detectedByKind = new Dictionary<TaskKind, int>();
        var acceptedByKind = new Dictionary<TaskKind, int>();
        var scannedTiles = 0;
        var capabilitySkippedTiles = 0;
        var duplicateTiles = 0;
        var noNavigationTiles = 0;
        var farmZones = 0;

        // BR-PRIO-03: no building pre-pass in U-13. Animals + building interiors deferred (TODO-05).
        foreach (var zone in contract.Zones)
        {
            if (zone.LocationName != "Farm") continue;
            farmZones++;

            for (var x = zone.TopLeft.X; x <= zone.BottomRight.X; x++)
            for (var y = zone.TopLeft.Y; y <= zone.BottomRight.Y; y++)
            {
                scannedTiles++;
                var tileVec  = new Vector2(x, y);
                var taskTile = new TileCoord(x, y);

                var task = DetectTask(tileVec, farm, enabled, snapshot,
                    out bool capabilitySkipped, out TaskKind? skippedKind);

                if (capabilitySkipped && skippedKind.HasValue)
                {
                    capabilitySkippedTiles++;
                    ModEntry.ModMonitor.Log($"[Dayswork][scan] capability skip {skippedKind.Value} at ({x},{y}).", LogLevel.Trace);
                }

                if (task is null) continue;

                Increment(detectedByKind, task.Value);
                taskTile = CanonicalTaskTile(task.Value, tileVec, farm);
                if (!seenWorkItems.Add((task.Value, taskTile)))
                {
                    duplicateTiles++;
                    continue;
                }

                var navTile = FindNavigationTile(taskTile, task.Value, tileVec, farm);
                if (navTile is null)
                {
                    noNavigationTiles++;
                    ModEntry.ModMonitor.Log($"[Dayswork][scan] no stand tile for {task.Value} at task ({taskTile.X},{taskTile.Y}) from scan ({x},{y}).", LogLevel.Trace);
                    continue;
                }

                Increment(acceptedByKind, task.Value);
                rawItems.Add((new WorkItem(navTile.Value, taskTile, task.Value), task.Value));
                ModEntry.ModMonitor.Log($"[Dayswork][scan] accepted {task.Value}: nav=({navTile.Value.X},{navTile.Value.Y}) task=({taskTile.X},{taskTile.Y}).", LogLevel.Trace);
            }
        }

        LogWorkScanSummary(contract, enabled, farmZones, scannedTiles, rawItems.Count,
            detectedByKind, acceptedByKind, capabilitySkippedTiles, noNavigationTiles, duplicateTiles);

        // User play-test feedback: outdoor tasks should route by nearest next item, not
        // by task kind. Animal tasks will regain first-priority handling in their future unit.
        return GreedyNearestNeighbour(rawItems.Select(r => r.item).ToList(), FarmEntrance);
    }

    /// <summary>
    /// Detects actionable work on a single tile.
    /// Returns the task kind, or null if the tile is not applicable.
    /// Sets capabilitySkipped/skippedKind when a tile is excluded by tool level.
    /// </summary>
    private TaskKind? DetectTask(
        Vector2 tileVec,
        GameLocation loc,
        IReadOnlySet<TaskKind> enabled,
        ToolSnapshot snapshot,
        out bool capabilitySkipped,
        out TaskKind? skippedKind)
    {
        capabilitySkipped = false;
        skippedKind       = null;

        // ── Terrain features ─────────────────────────────────────────────────

        if (loc.terrainFeatures.TryGetValue(tileVec, out var tf))
        {
            if (tf is HoeDirt dirt && dirt.crop is not null)
            {
                if (enabled.Contains(TaskKind.HarvestCrops) &&
                    !dirt.crop.dead.Value &&
                    IsReadyToHarvest(dirt.crop))
                    return TaskKind.HarvestCrops;

                if (enabled.Contains(TaskKind.WaterCrops) &&
                    dirt.state.Value != HoeDirt.watered &&
                    !dirt.crop.dead.Value &&
                    dirt.crop.currentPhase.Value < dirt.crop.phaseDays.Count - 1)
                    return TaskKind.WaterCrops;

                return null; // dead or not ready — FR-SKIP-05 / TODO-02
            }

            if (tf is FruitTree fruitTree && fruitTree.fruit.Count > 0 &&
                enabled.Contains(TaskKind.CollectFruit))
                return TaskKind.CollectFruit;

            if (tf is Tree && enabled.Contains(TaskKind.CutTrees))
            {
                // Capability gate — FR-SKIP-01/03.
                var axeTarget = ObjectTargetClassifier.ClassifyAxe(tileVec, loc);
                if (axeTarget is null) return null;
                if (!_capability.CanChop(snapshot, axeTarget.Value))
                {
                    capabilitySkipped = true;
                    skippedKind       = TaskKind.CutTrees;
                    return null;
                }
                return TaskKind.CutTrees;
            }
        }

        if (enabled.Contains(TaskKind.CutTrees) &&
            ObjectTargetClassifier.ClassifyAxe(tileVec, loc) is { } clumpAxeTarget &&
            clumpAxeTarget != AxeTarget.FruitTree)
        {
            if (!_capability.CanChop(snapshot, clumpAxeTarget))
            {
                capabilitySkipped = true;
                skippedKind       = TaskKind.CutTrees;
                return null;
            }

            return TaskKind.CutTrees;
        }

        // ── Placed objects ────────────────────────────────────────────────────

        if (loc.objects.TryGetValue(tileVec, out var obj))
        {
            if (obj.IsWeeds() && enabled.Contains(TaskKind.ClearWeeds))
                return TaskKind.ClearWeeds;

            if (enabled.Contains(TaskKind.ClearRocks))
            {
                var pickTarget = ObjectTargetClassifier.ClassifyPick(tileVec, loc);
                if (pickTarget is not null)
                {
                    if (!_capability.CanBreak(snapshot, pickTarget.Value))
                    {
                        capabilitySkipped = true;
                        skippedKind       = TaskKind.ClearRocks;
                        return null;
                    }
                    return TaskKind.ClearRocks;
                }
            }

            if (obj.Name == "Twig" && enabled.Contains(TaskKind.CutTrees))
                return TaskKind.CutTrees;
        }

        // ── ResourceClumps (large boulders / meteorites) ─────────────────────
        // ObjectTargetClassifier.ClassifyPick also checks resource clumps.
        if (enabled.Contains(TaskKind.ClearRocks) && !loc.objects.ContainsKey(tileVec))
        {
            var pickTarget = ObjectTargetClassifier.ClassifyPick(tileVec, loc);
            if (pickTarget is not null)
            {
                if (!_capability.CanBreak(snapshot, pickTarget.Value))
                {
                    capabilitySkipped = true;
                    skippedKind       = TaskKind.ClearRocks;
                    return null;
                }
                return TaskKind.ClearRocks;
            }
        }

        if (loc.terrainFeatures.TryGetValue(tileVec, out var grassFeature) &&
            grassFeature is Grass &&
            enabled.Contains(TaskKind.ClearGrass))
            return TaskKind.ClearGrass;

        return null;
    }

    private static bool IsReadyToHarvest(Crop crop) =>
        crop.currentPhase.Value >= crop.phaseDays.Count - 1 && !crop.dead.Value;

    private static bool IsTrellisCrop(Vector2 tileVec, GameLocation loc)
    {
        if (!loc.terrainFeatures.TryGetValue(tileVec, out var tf) || tf is not HoeDirt dirt)
            return false;
        return dirt.crop?.raisedSeeds.Value == true;
    }

    private static TileCoord CanonicalTaskTile(TaskKind task, Vector2 tileVec, Farm farm)
    {
        if (task is TaskKind.CutTrees or TaskKind.ClearRocks &&
            ObjectTargetClassifier.FindResourceClumpAt(tileVec, farm) is { } clump)
            return new TileCoord((int)clump.Tile.X, (int)clump.Tile.Y);

        return new TileCoord((int)tileVec.X, (int)tileVec.Y);
    }

    private static TileCoord? FindNavigationTile(TileCoord taskTile, TaskKind task, Vector2 tileVec, Farm farm)
    {
        if (ObjectTargetClassifier.FindResourceClumpAt(tileVec, farm) is { } clump)
            return FindOrthogonalNeighbour(clump, farm);

        if (!RequiresAdjacentNavigation(task) &&
            !IsTrellisCrop(tileVec, farm) &&
            IsTileReachable(taskTile, farm))
            return taskTile;

        return FindOrthogonalNeighbour(taskTile, farm);
    }

    private static bool RequiresAdjacentNavigation(TaskKind task) =>
        task is TaskKind.CollectFruit
             or TaskKind.CutTrees
             or TaskKind.ClearRocks
             or TaskKind.ClearWeeds;

    private static void Increment(Dictionary<TaskKind, int> counts, TaskKind kind) =>
        counts[kind] = counts.TryGetValue(kind, out var existing) ? existing + 1 : 1;

    private static string FormatCounts(Dictionary<TaskKind, int> counts) =>
        counts.Count == 0
            ? "none"
            : string.Join(", ", counts.OrderBy(kvp => kvp.Key).Select(kvp => $"{kvp.Key}={kvp.Value}"));

    private static void LogWorkScanSummary(
        Contract contract,
        IReadOnlySet<TaskKind> enabled,
        int farmZones,
        int scannedTiles,
        int acceptedItems,
        Dictionary<TaskKind, int> detectedByKind,
        Dictionary<TaskKind, int> acceptedByKind,
        int capabilitySkippedTiles,
        int noNavigationTiles,
        int duplicateTiles)
    {
        var message =
            $"[Dayswork][scan] contract={contract.Id} farmZones={farmZones} scannedTiles={scannedTiles} " +
            $"enabled={string.Join(",", enabled.OrderBy(t => t))} detected=[{FormatCounts(detectedByKind)}] " +
            $"accepted=[{FormatCounts(acceptedByKind)}] acceptedItems={acceptedItems} " +
            $"capabilitySkipped={capabilitySkippedTiles} noStandTile={noNavigationTiles} duplicateClumpTiles={duplicateTiles}";

        ModEntry.ModMonitor.Log(message, acceptedItems == 0 ? LogLevel.Info : LogLevel.Debug);
    }

    private static TileCoord? FindOrthogonalNeighbour(TileCoord tile, Farm farm)
    {
        TileCoord[] candidates =
        {
            new(tile.X,     tile.Y - 1), // N
            new(tile.X + 1, tile.Y),     // E
            new(tile.X,     tile.Y + 1), // S
            new(tile.X - 1, tile.Y),     // W
        };

        foreach (var c in candidates)
        {
            if (WorkerMovementDriver.IsTilePassableForWorker(new Point(c.X, c.Y), farm))
                return c;
        }
        return null;
    }

    private static TileCoord? FindOrthogonalNeighbour(ResourceClump clump, Farm farm)
    {
        var minX = (int)clump.Tile.X;
        var minY = (int)clump.Tile.Y;
        var maxX = minX + clump.width.Value - 1;
        var maxY = minY + clump.height.Value - 1;

        var candidates = new List<TileCoord>();
        for (var x = minX; x <= maxX; x++)
        {
            candidates.Add(new TileCoord(x, minY - 1));
            candidates.Add(new TileCoord(x, maxY + 1));
        }

        for (var y = minY; y <= maxY; y++)
        {
            candidates.Add(new TileCoord(minX - 1, y));
            candidates.Add(new TileCoord(maxX + 1, y));
        }

        foreach (var c in candidates.Distinct())
        {
            if (WorkerMovementDriver.IsTilePassableForWorker(new Point(c.X, c.Y), farm))
                return c;
        }

        return null;
    }

    /// <summary>
    /// Greedy nearest-neighbour sort: starting from <paramref name="origin"/>, repeatedly
    /// picks the closest remaining item (Manhattan distance to TaskTile) and appends it.
    /// O(n²) — acceptable for farm-scale work lists (typically &lt;400 tiles).
    /// </summary>
    private static List<WorkItem> GreedyNearestNeighbour(List<WorkItem> items, TileCoord origin)
    {
        if (items.Count == 0) return items;

        var remaining = new List<WorkItem>(items);
        var sorted    = new List<WorkItem>(items.Count);
        var current   = origin;

        while (remaining.Count > 0)
        {
            int bestIdx  = 0;
            int bestDist = int.MaxValue;

            for (int i = 0; i < remaining.Count; i++)
            {
                int dist = Math.Abs(remaining[i].TaskTile.X - current.X)
                         + Math.Abs(remaining[i].TaskTile.Y - current.Y);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestIdx  = i;
                }
            }

            var chosen = remaining[bestIdx];
            remaining.RemoveAt(bestIdx);
            sorted.Add(chosen);
            current = chosen.NavTile;
        }

        return sorted;
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
