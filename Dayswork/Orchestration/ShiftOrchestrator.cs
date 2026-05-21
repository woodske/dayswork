using Dayswork.Core.Capabilities;
using Dayswork.Core.Config;
using Dayswork.Core.Domain;
using Dayswork.Core.Shifts;
using Dayswork.Integration;
using Dayswork.Worker;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
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
    private const int DelayedTreeDebrisSweepTicks = 240;
    private const int DelayedTreeDebrisSweepRadiusTiles = 6;

    private readonly ToolLevelReader      _toolReader;
    private readonly ToolSwapAnimator     _toolAnimator;
    private readonly ICapabilityEvaluator _capability      = new CapabilityEvaluator();
    private readonly ITaskPriorityOrderer _priorityOrderer = new TaskPriorityOrderer();
    private readonly IConfigSnapshot      _config;
    private readonly WorkerMovementDriver _nav = new();

    private ShiftContext? _ctx;
    private FarmhandNpc?  _farmhand;
    private int           _tickCount;
    private int           _morningEntranceHoldTicks;
    private bool          _exitWalkStarted;

    // Per-WorkItem state — the nav tile and task tile are tracked separately (trellis crops).
    private bool      _actionPending;
    private TaskKind  _pendingTask;
    private TileCoord _pendingNavTile;
    private TileCoord _pendingTaskTile;

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
        ToolSwapAnimator toolAnimator)
    {
        _toolReader   = toolReader;
        _config       = config;
        _toolAnimator = toolAnimator;
        _stuck        = new StuckDetector(config.StuckInitialWaitMinutes);
    }

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

    public void StartShift(Contract contract)
    {
        if (_ctx is not null)
        {
            ModEntry.ModMonitor.Log("[Dayswork] StartShift called while a shift is already active — ignoring.", LogLevel.Warn);
            return;
        }

        var farm     = Game1.getFarm();
        var snapshot = _toolReader.ReadSnapshot(Game1.player);

        // Build work list before creating ShiftContext so we can pass it in.
        var workList = BuildWorkList(contract, farm, snapshot, out var toolMissingWarnings);

        if (workList.Count == 0)
        {
            ModEntry.ModMonitor.Log("[Dayswork] No applicable work found for today's contract — skipping shift.", LogLevel.Info);
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
        _exitWalkStarted     = false;
        _morningEntranceHoldTicks = MorningEntranceHoldTicks;
        _pendingDebrisSweeps.Clear();

        var firstItem = workList[0];
        var remaining = workList.Skip(1);

        _ctx = new ShiftContext(
            contractId:     contract.Id,
            zones:          contract.Zones,
            enabledTasks:   contract.EnabledTasks,
            depositAmount:  contract.DepositAmount,
            hourlyRate:     contract.HourlyRate,
            toolSnapshot:   snapshot,
            workList:       remaining,
            shiftStartTime: Game1.timeOfDay);

        // Populate tool-missing warnings collected during BuildWorkList.
        foreach (var kind in toolMissingWarnings)
            _ctx.ToolMissingWarnings.Add(kind);

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
                HandleDeposit(farm);
                break;
            case IntentExitFarm:
                HandleExit(farm);
                break;
        }
    }

    public void OnSaving(object? sender, SavingEventArgs e)
    {
        if (_farmhand is null) return;

        var farm = Game1.getFarm();

        if (_ctx is not null)
        {
            FlushPendingDebrisSweeps();
            if (_ctx.ShiftEndTime.HasValue)
            {
                // Shift ended normally (8pm cap / work-complete / stuck step 3 / EndShiftEarly).
                // The worker was just mid-cleanup (walking to bin or exit) when the player slept.
                // Flush any buffered items that haven't reached the bin yet, then give the correct
                // partial refund. No warning — this is expected end-of-day behaviour.
                var items = _ctx.Buffer.TakeAll();
                foreach (var (itemId, qty) in items)
                {
                    var obj = ItemRegistry.Create(itemId, qty);
                    if (obj is not null)
                        farm.getShippingBin(Game1.player).Add(obj);
                }
                var refund = _ctx.ComputeRefund();
                if (refund > 0)
                    Game1.player.Money += refund;
            }
            else
            {
                // Genuine mid-shift interruption (player saved mid-day without sleeping).
                // Worker didn't finish — refund the full deposit.
                ModEntry.ModMonitor.Log("[Dayswork] Shift interrupted by save — removing worker and refunding deposit.", LogLevel.Warn);
                if (_ctx.DepositAmount > 0)
                    Game1.player.Money += _ctx.DepositAmount;
            }
        }

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
        if (_nav.NavigationFailed || _nav.HasArrived)
        {
            var items = _ctx!.Buffer.TakeAll();
            foreach (var (itemId, qty) in items)
            {
                var obj = ItemRegistry.Create(itemId, qty);
                if (obj is not null)
                    farm.getShippingBin(Game1.player).Add(obj);
            }
            _ctx.StateMachine.Transition(ShiftPhase.Exiting, new IntentExitFarm());
            _exitWalkStarted = false;
            _nav.StartNavigation(FarmEntrance, farm, _farmhand!);
        }
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
        if (refund > 0)
            Game1.player.Money += refund;

        ModEntry.ModMonitor.Log(
            $"[Dayswork] Shift complete. Hours: {((_ctx.ShiftEndTime ?? Game1.timeOfDay) - _ctx.ShiftStartTime) / 60}. Refund: {refund}g.",
            LogLevel.Info);

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
        FlushPendingDebrisSweeps();
        _ctx!.StateMachine.Transition(ShiftPhase.Depositing, new IntentDepositInShippingBin());
        _nav.StartNavigation(ShippingBinTile, farm, _farmhand!);
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
        _exitWalkStarted = false;
        _pendingDebrisSweeps.Clear();
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
        CollectNewDebris(before, loc);
    }

    private void InvokeCollectFruit(TileCoord tile, GameLocation loc)
    {
        var tileVec = new Vector2(tile.X, tile.Y);
        if (!loc.terrainFeatures.TryGetValue(tileVec, out var tf) || tf is not FruitTree tree) return;
        var before = new HashSet<Debris>(loc.debris);
        tree.shake(tileVec, false);
        CollectNewDebris(before, loc);
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
        CollectNewDebris(before, loc);
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
            if (!CollectNewDebris(beforeClump, loc))
                _ctx.Buffer.Add("(O)390", 10);
            return;
        }

        if (!loc.objects.TryGetValue(tileVec, out var obj)) return;
        if (ObjectTargetClassifier.ClassifyPick(tileVec, loc) is null) return;

        var before  = new HashSet<Debris>(loc.debris);
        var actionRemoved = obj.performToolAction(pickaxe);
        if (loc.objects.ContainsKey(tileVec))
            loc.removeObject(tileVec, false);
        ModEntry.ModMonitor.Log(
            $"[Dayswork][action] clear rock at ({tile.X},{tile.Y}) performToolAction={actionRemoved} removed={!loc.objects.ContainsKey(tileVec)}.",
            LogLevel.Debug);
        if (!CollectNewDebris(before, loc))
            _ctx!.Buffer.Add("(O)390", 1);
    }

    private void InvokeCutTree(TileCoord tile, GameLocation loc)
    {
        var tileVec = new Vector2(tile.X, tile.Y);
        var axe = new Axe { UpgradeLevel = (int)_ctx!.ToolSnapshot.AxeLevel, lastUser = Game1.player };

        if (loc.terrainFeatures.TryGetValue(tileVec, out var tf) && tf is Tree tree)
        {
            bool isMahogany = tree.treeType.Value == Tree.mahoganyTree;
            bool wasStump   = tree.stump.Value;
            var  before     = new HashSet<Debris>(loc.debris);
            var  removeTree = tree.performToolAction(axe, 0, tileVec);
            if (removeTree && loc.terrainFeatures.ContainsKey(tileVec))
                loc.terrainFeatures.Remove(tileVec);
            ModEntry.ModMonitor.Log(
                $"[Dayswork][action] cut tree at ({tile.X},{tile.Y}) remove={removeTree} health={tree.health.Value:0.##} stump={tree.stump.Value}.",
                LogLevel.Debug);
            var collected = CollectNewDebris(before, loc);
            if (removeTree && !collected)
                _ctx!.Buffer.Add(isMahogany ? "(O)709" : "(O)388", 8);
            if (!wasStump && !removeTree)
                QueueDelayedDebrisSweep(loc, tileVec, before);
            return;
        }

        if (ObjectTargetClassifier.FindResourceClumpAt(tileVec, loc) is { } clump)
        {
            var before = new HashSet<Debris>(loc.debris);
            clump.performToolAction(axe, 0, clump.Tile);
            loc.resourceClumps.Remove(clump);

            if (!CollectNewDebris(before, loc))
            {
                var fallbackQty = clump.parentSheetIndex.Value == ResourceClump.hollowLogIndex ? 8 : 2;
                _ctx!.Buffer.Add("(O)709", fallbackQty);
            }

            return;
        }

        if (loc.objects.TryGetValue(tileVec, out var obj) && obj.Name == "Twig")
        {
            var before = new HashSet<Debris>(loc.debris);
            obj.performToolAction(axe);
            if (loc.objects.ContainsKey(tileVec))
                loc.removeObject(tileVec, false);
            CollectNewDebris(before, loc);
        }
    }

    private bool CollectNewDebris(
        HashSet<Debris> before,
        GameLocation loc,
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

            _ctx!.Buffer.Add(itemId, stack);
            loc.debris.Remove(d);
            collected = true;
        }
        return collected;
    }

    private static bool TryGetDebrisItem(Debris debris, out string itemId, out int stack)
    {
        if (debris.item is not null)
        {
            itemId = debris.item.ItemId;
            stack  = Math.Max(1, debris.item.Stack);
            return true;
        }

        stack = Math.Max(1, debris.Chunks?.Count ?? 1);
        itemId = debris.chunkType.Value switch
        {
            Debris.woodDebris => "(O)388",
            Debris.bigWoodDebris => "(O)709",
            Debris.stoneDebris or Debris.bigStoneDebris => "(O)390",
            Debris.coalDebris => "(O)382",
            Debris.copperDebris => "(O)378",
            Debris.ironDebris => "(O)380",
            Debris.goldDebris => "(O)384",
            Debris.iridiumDebris => "(O)386",
            _ => "",
        };

        return itemId.Length > 0;
    }

    private void QueueDelayedDebrisSweep(GameLocation loc, Vector2 tileVec, HashSet<Debris> baseline)
    {
        var origin = new Vector2(tileVec.X * 64f + 32f, tileVec.Y * 64f + 32f);
        _pendingDebrisSweeps.Add(new PendingDebrisSweep(
            loc,
            origin,
            baseline,
            DelayedTreeDebrisSweepTicks,
            DelayedTreeDebrisSweepRadiusTiles));
    }

    private void ProcessPendingDebrisSweeps()
    {
        for (var i = _pendingDebrisSweeps.Count - 1; i >= 0; i--)
        {
            var sweep = _pendingDebrisSweeps[i];
            CollectNewDebris(sweep.Baseline, sweep.Location, sweep.Origin, sweep.RadiusTiles);
            sweep.TicksRemaining--;
            if (sweep.TicksRemaining <= 0)
                _pendingDebrisSweeps.RemoveAt(i);
        }
    }

    private void FlushPendingDebrisSweeps()
    {
        foreach (var sweep in _pendingDebrisSweeps)
            CollectNewDebris(sweep.Baseline, sweep.Location, sweep.Origin, sweep.RadiusTiles);

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

        return debris.Chunks.Count == 0;
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
        ToolSnapshot snapshot,
        out HashSet<TaskKind> toolMissingWarnings)
    {
        toolMissingWarnings = new HashSet<TaskKind>();
        var enabled = contract.EnabledTasks;
        // Track which task kinds had at least one skippable tile; intersected with "entire type absent"
        // at end to determine BR-TOOL-02 warnings.
        var capSkippedKinds = new HashSet<TaskKind>();
        var anyItemForKind  = new HashSet<TaskKind>();
        var seenWorkItems   = new HashSet<(TaskKind Task, TileCoord Tile)>();

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
                    capSkippedKinds.Add(skippedKind.Value);
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

                anyItemForKind.Add(task.Value);
                Increment(acceptedByKind, task.Value);
                rawItems.Add((new WorkItem(navTile.Value, taskTile, task.Value), task.Value));
                ModEntry.ModMonitor.Log($"[Dayswork][scan] accepted {task.Value}: nav=({navTile.Value.X},{navTile.Value.Y}) task=({taskTile.X},{taskTile.Y}).", LogLevel.Trace);
            }
        }

        // BR-TOOL-02: a task kind is "missing tool" only if every tile was capability-skipped
        // (i.e. the kind appears in capSkipped but produced no items).
        foreach (var kind in capSkippedKinds)
            if (!anyItemForKind.Contains(kind))
                toolMissingWarnings.Add(kind);

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
            int radiusTiles)
        {
            Location = location;
            Origin = origin;
            Baseline = baseline;
            TicksRemaining = ticksRemaining;
            RadiusTiles = radiusTiles;
        }

        public GameLocation Location { get; }
        public Vector2 Origin { get; }
        public HashSet<Debris> Baseline { get; }
        public int TicksRemaining { get; set; }
        public int RadiusTiles { get; }
    }
}
