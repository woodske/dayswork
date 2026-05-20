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
    private static readonly TileCoord FarmEntrance = new(71, 14);
    // Shipping bin tile on Standard Farm.
    private static readonly TileCoord ShippingBinTile = new(71, 13);

    private readonly ToolLevelReader _toolReader;
    private readonly PathFindControllerAdapter _nav = new();

    private ShiftContext? _ctx;
    private FarmhandNpc? _farmhand;
    private int _tickCount;
    private bool _actionPending;

    public ShiftOrchestrator(ToolLevelReader toolReader)
    {
        _toolReader = toolReader;
    }

    // ── Public API ───────────────────────────────────────────────────────────

    public void StartShift(Contract contract)
    {
        if (_ctx is not null)
        {
            ModEntry.ModMonitor.Log("[Dayswork] StartShift called while a shift is already active — ignoring.", LogLevel.Warn);
            return;
        }

        var farm     = Game1.getFarm();
        var snapshot = _toolReader.ReadSnapshot(Game1.player);
        var workList = BuildWorkList(contract, farm);

        if (workList.Count == 0)
        {
            ModEntry.ModMonitor.Log("[Dayswork] No applicable work found for today's contract — skipping shift.", LogLevel.Info);
            return;
        }

        var spawnPos = new Vector2(FarmEntrance.X, FarmEntrance.Y) * 64f;
        _farmhand = new FarmhandNpc(spawnPos);
        farm.addCharacter(_farmhand);

        var firstItem = workList[0];
        var remaining = workList.Skip(1);

        _ctx = new ShiftContext(
            contractId:    contract.Id,
            zones:         contract.Zones,
            enabledTasks:  contract.EnabledTasks,
            depositAmount: contract.DepositAmount,
            hourlyRate:    contract.HourlyRate,
            toolSnapshot:  snapshot,
            workList:      remaining,
            shiftStartTime: Game1.timeOfDay);

        _pendingTask = firstItem.Task;
        _ctx.StateMachine.Transition(ShiftPhase.Working, new IntentMoveToTile(firstItem.Tile));
        _nav.StartNavigation(firstItem.Tile, farm, _farmhand);
        _actionPending = false;
    }

    // ── SMAPI event handlers ─────────────────────────────────────────────────

    public void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
    {
        if (_ctx is null || _ctx.StateMachine.Phase == ShiftPhase.Done) return;
        if (++_tickCount % 4 != 0) return; // Throttled-Tick pattern (N1: B)

        var farm = Game1.getFarm();
        switch (_ctx.StateMachine.CurrentIntent)
        {
            case IntentMoveToTile:
                HandleMovement(farm);
                break;
            case IntentPerformTaskAt intent:
                HandleTaskAction(intent, farm);
                break;
            case IntentDepositInShippingBin:
                HandleDeposit(farm);
                break;
            case IntentExitFarm:
                HandleExit(farm);
                break;
        }
    }

    public void OnSaving(object? sender, StardewModdingAPI.Events.SavingEventArgs e)
    {
        // If the worker is still active at save time, remove them and refund the full deposit.
        // This prevents FarmhandNpc from being written into the save file (it has no
        // parameterless constructor that reconstructs a valid NPC on load).
        if (_farmhand is null) return;

        ModEntry.ModMonitor.Log("[Dayswork] Shift interrupted by save — removing worker and refunding deposit.", LogLevel.Warn);
        var farm = Game1.getFarm();
        farm.characters.Remove(_farmhand);
        if (_ctx is not null && _ctx.DepositAmount > 0)
            Game1.player.Money += _ctx.DepositAmount;

        _farmhand = null;
        _nav.Clear();
        _ctx = null;
    }

    public void OnTimeChanged(object? sender, TimeChangedEventArgs e)
    {
        // 8pm hard cap (BR-12).
        if (_ctx is null) return;
        if (e.NewTime >= 2000 && _ctx.StateMachine.Phase == ShiftPhase.Working)
        {
            ModEntry.ModMonitor.Log("[Dayswork] 8pm cap reached — transitioning to deposit.", LogLevel.Trace);
            _ctx.ShiftEndTime = e.NewTime;
            BeginDeposit();
        }
    }

    // ── Intent handlers ──────────────────────────────────────────────────────

    private void HandleMovement(Farm farm)
    {
        if (_nav.NavigationFailed)
        {
            // Skip-and-Continue pattern (Pattern 4): unreachable tile.
            ModEntry.ModMonitor.Log($"[Dayswork] Tile unreachable — skipping.", LogLevel.Trace);
            AdvanceWorkList(farm);
            return;
        }

        if (_nav.HasArrived)
        {
            // Arrived at target tile — transition to task action.
            if (_ctx!.StateMachine.CurrentIntent is IntentMoveToTile move)
            {
                var tile = move.Destination;
                var task = _ctx.WorkList.Count > 0 ? _ctx.WorkList.Peek().Task : TaskKind.ClearWeeds;
                // Find the task for this tile from context.
                // The task kind is embedded in the next WorkItem already consumed when we set the intent.
                // We need to retrieve it from when we set the intent — store it in context.
                // Refactored: use a pending task field instead.
                _ctx.StateMachine.SetIntent(new IntentPerformTaskAt(tile, _pendingTask));
                _actionPending = false;
            }
        }
    }

    private TaskKind _pendingTask;

    private void HandleTaskAction(IntentPerformTaskAt intent, Farm farm)
    {
        if (!_actionPending)
        {
            InvokeTaskAction(intent.Tile, intent.Task, farm);
            _actionPending = true;
            return;
        }

        // Poll for completion (Invoke-and-Poll pattern, N2: B).
        if (IsTaskComplete(intent.Tile, intent.Task, farm))
        {
            _actionPending = false;
            AdvanceWorkList(farm);
        }
    }

    private void HandleDeposit(Farm farm)
    {
        if (_nav.NavigationFailed || _nav.HasArrived)
        {
            // At or unable to reach shipping bin — dump everything.
            var items = _ctx!.Buffer.TakeAll();
            foreach (var (itemId, qty) in items)
            {
                var obj = ItemRegistry.Create(itemId, qty);
                if (obj is not null)
                    farm.getShippingBin(Game1.player).Add(obj);
            }
            _ctx.StateMachine.Transition(ShiftPhase.Exiting, new IntentExitFarm());
            _nav.StartNavigation(FarmEntrance, farm, _farmhand!);
        }
    }

    private void HandleExit(Farm farm)
    {
        if (_nav.NavigationFailed || _nav.HasArrived)
        {
            var refund = _ctx!.ComputeRefund();
            if (refund > 0)
                Game1.player.Money += refund;

            ModEntry.ModMonitor.Log(
                $"[Dayswork] Shift complete. Hours worked: {((_ctx.ShiftEndTime ?? Game1.timeOfDay) - _ctx.ShiftStartTime) / 60}. Refund: {refund}g.",
                LogLevel.Info);

            farm.characters.Remove(_farmhand);
            _farmhand = null;
            _nav.Clear();
            _ctx.StateMachine.Transition(ShiftPhase.Done);
            _ctx = null;
        }
    }

    // ── Work list ────────────────────────────────────────────────────────────

    private void AdvanceWorkList(Farm farm)
    {
        if (_ctx!.WorkList.Count == 0)
        {
            // Work complete — begin deposit run.
            _ctx.ShiftEndTime = Game1.timeOfDay;
            BeginDeposit();
            return;
        }

        var next = _ctx.WorkList.Dequeue();
        _pendingTask = next.Task;
        _ctx.StateMachine.SetIntent(new IntentMoveToTile(next.Tile));
        _nav.StartNavigation(next.Tile, farm, _farmhand!);
    }

    private void BeginDeposit()
    {
        var farm = Game1.getFarm();
        _ctx!.StateMachine.Transition(ShiftPhase.Depositing, new IntentDepositInShippingBin());
        _nav.StartNavigation(ShippingBinTile, farm, _farmhand!);
    }

    // ── Task invocation (Invoke-and-Poll, N2: B) ─────────────────────────────

    private void InvokeTaskAction(TileCoord tile, TaskKind task, Farm farm)
    {
        switch (task)
        {
            case TaskKind.WaterCrops:
                InvokeWater(tile, farm);
                break;
            case TaskKind.HarvestCrops:
                InvokeHarvest(tile, farm);
                break;
            case TaskKind.CollectFruit:
                InvokeCollectFruit(tile, farm);
                break;
            case TaskKind.ClearWeeds:
                InvokeClearWeed(tile, farm);
                break;
            case TaskKind.ClearGrass:
                InvokeClearGrass(tile, farm);
                break;
            case TaskKind.ClearRocks:
                InvokeClearRock(tile, farm);
                break;
            case TaskKind.CutTrees:
                InvokeCutTree(tile, farm);
                break;
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

        dirt.crop.harvest(tile.X, tile.Y, dirt, null);
        CollectDebrisAt(tile, loc);
    }

    private void InvokeCollectFruit(TileCoord tile, GameLocation loc)
    {
        var tileVec = new Vector2(tile.X, tile.Y);
        if (!loc.terrainFeatures.TryGetValue(tileVec, out var tf) || tf is not FruitTree tree) return;
        tree.shake(tileVec, false);
        CollectDebrisAt(tile, loc);
    }

    private void InvokeClearWeed(TileCoord tile, GameLocation loc)
    {
        var tileVec = new Vector2(tile.X, tile.Y);
        if (!loc.objects.TryGetValue(tileVec, out var obj) || !obj.IsWeeds()) return;
        // performToolAction signals success but doesn't remove the object — the caller
        // (normally Pickaxe/Scythe DoFunction) does the removal. We do it explicitly.
        var scythe = new MeleeWeapon("66") { lastUser = Game1.player };
        obj.performToolAction(scythe);
        if (loc.objects.ContainsKey(tileVec))
            loc.removeObject(tileVec, false);
        CollectDebrisAt(tile, loc);
    }

    private void InvokeClearGrass(TileCoord tile, GameLocation loc)
    {
        var tileVec = new Vector2(tile.X, tile.Y);
        if (!loc.terrainFeatures.TryGetValue(tileVec, out var tf) || tf is not Grass grass) return;
        var scythe = new MeleeWeapon("66") { lastUser = Game1.player };
        grass.performToolAction(scythe, 0, tileVec);
        // Hay routing (BR-10): Stardew handles silo deposit internally via grass action.
        if (loc.terrainFeatures.ContainsKey(tileVec))
            loc.terrainFeatures.Remove(tileVec);
    }

    private void InvokeClearRock(TileCoord tile, GameLocation loc)
    {
        var tileVec = new Vector2(tile.X, tile.Y);
        if (!loc.objects.TryGetValue(tileVec, out var obj)) return;
        var pickaxe = new Pickaxe { UpgradeLevel = (int)_ctx!.ToolSnapshot.PickaxeLevel, lastUser = Game1.player };
        obj.performToolAction(pickaxe);
        if (loc.objects.ContainsKey(tileVec))
            loc.removeObject(tileVec, false);
        CollectDebrisAt(tile, loc);
    }

    private void InvokeCutTree(TileCoord tile, GameLocation loc)
    {
        var tileVec = new Vector2(tile.X, tile.Y);
        var axe = new Axe { UpgradeLevel = (int)_ctx!.ToolSnapshot.AxeLevel, lastUser = Game1.player };

        // Standing tree (terrain feature).
        if (loc.terrainFeatures.TryGetValue(tileVec, out var tf) && tf is Tree tree)
        {
            tree.performToolAction(axe, 0, tileVec);
            if (loc.terrainFeatures.ContainsKey(tileVec))
                loc.terrainFeatures.Remove(tileVec);
            CollectDebrisAt(tile, loc);
            return;
        }

        // Twig/branch log lying on the ground — same axe action, stored in objects not terrainFeatures.
        if (loc.objects.TryGetValue(tileVec, out var obj) && obj.Name == "Twig")
        {
            obj.performToolAction(axe);
            if (loc.objects.ContainsKey(tileVec))
                loc.removeObject(tileVec, false);
            CollectDebrisAt(tile, loc);
        }
    }

    // Collect any debris items that appeared near the tile and move them to buffer.
    private void CollectDebrisAt(TileCoord tile, GameLocation loc)
    {
        var tileCenter = new Vector2(tile.X * 64f + 32f, tile.Y * 64f + 32f);
        var toRemove = new List<Debris>();
        foreach (var debris in loc.debris)
        {
            if (debris.Chunks.Count == 0) continue;
            var chunk = debris.Chunks[0];
            var dist  = Vector2.Distance(chunk.position.Value, tileCenter);
            if (dist > 128f) continue; // only collect nearby drops

            if (debris.item is not null)
            {
                _ctx!.Buffer.Add(debris.item.ItemId, debris.item.Stack);
                toRemove.Add(debris);
            }
        }
        foreach (var d in toRemove)
            loc.debris.Remove(d);
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
                !loc.objects.ContainsKey(tileVec),

            TaskKind.ClearGrass or TaskKind.CutTrees =>
                !loc.terrainFeatures.ContainsKey(tileVec),

            _ => true,
        };
    }

    // ── Work list construction ────────────────────────────────────────────────

    private static List<WorkItem> BuildWorkList(Contract contract, Farm farm)
    {
        var enabled  = contract.EnabledTasks;
        var workList = new List<WorkItem>();

        // Building pre-pass: tile-based tasks inside building interiors.
        foreach (var building in farm.buildings)
        {
            var indoors = building.indoors.Value;
            if (indoors is null) continue;

            var w = indoors.map.Layers[0].LayerWidth;
            var h = indoors.map.Layers[0].LayerHeight;
            for (var x = 0; x < w; x++)
            for (var y = 0; y < h; y++)
            {
                var task = DetectTask(new Vector2(x, y), indoors, enabled);
                if (task.HasValue)
                    workList.Add(new WorkItem(new TileCoord(x, y), task.Value));
            }
        }

        // Open-farm tiles — nearest-first from farm entrance.
        var openFarm = new List<WorkItem>();
        foreach (var zone in contract.Zones)
        {
            if (zone.LocationName != "Farm") continue;
            for (var x = zone.TopLeft.X; x <= zone.BottomRight.X; x++)
            for (var y = zone.TopLeft.Y; y <= zone.BottomRight.Y; y++)
            {
                var task = DetectTask(new Vector2(x, y), farm, enabled);
                if (task.HasValue)
                    openFarm.Add(new WorkItem(new TileCoord(x, y), task.Value));
            }
        }

        // Sort open-farm tiles by Manhattan distance from farm entrance.
        openFarm.Sort((a, b) =>
        {
            var da = Math.Abs(a.Tile.X - FarmEntrance.X) + Math.Abs(a.Tile.Y - FarmEntrance.Y);
            var db = Math.Abs(b.Tile.X - FarmEntrance.X) + Math.Abs(b.Tile.Y - FarmEntrance.Y);
            return da.CompareTo(db);
        });

        workList.AddRange(openFarm);
        return workList;
    }

    private static TaskKind? DetectTask(Vector2 tileVec, GameLocation loc, IReadOnlySet<TaskKind> enabled)
    {
        // Check terrain features.
        if (loc.terrainFeatures.TryGetValue(tileVec, out var tf))
        {
            if (tf is HoeDirt dirt && dirt.crop is not null)
            {
                if (enabled.Contains(TaskKind.HarvestCrops) && (dirt.crop.fullyGrown.Value || IsReadyToHarvest(dirt.crop)))
                    return TaskKind.HarvestCrops;
                if (enabled.Contains(TaskKind.WaterCrops) && dirt.state.Value != HoeDirt.watered && dirt.crop.currentPhase.Value < dirt.crop.phaseDays.Count - 1)
                    return TaskKind.WaterCrops;
            }
            if (tf is FruitTree fruitTree && fruitTree.fruit.Count > 0 && enabled.Contains(TaskKind.CollectFruit))
                return TaskKind.CollectFruit;
            if (tf is Grass && enabled.Contains(TaskKind.ClearGrass))
                return TaskKind.ClearGrass;
            if (tf is Tree && enabled.Contains(TaskKind.CutTrees))
                return TaskKind.CutTrees;
        }

        // Check placed objects.
        if (loc.objects.TryGetValue(tileVec, out var obj))
        {
            if (obj.IsWeeds() && enabled.Contains(TaskKind.ClearWeeds))
                return TaskKind.ClearWeeds;
            // U-10: match basic stones only. Ore nodes/boulders with tool-level gates are U-13 scope.
            if (obj.Name == "Stone" && enabled.Contains(TaskKind.ClearRocks))
                return TaskKind.ClearRocks;
            // Twig/branch logs on the ground are cleared with an axe — map to CutTrees.
            if (obj.Name == "Twig" && enabled.Contains(TaskKind.CutTrees))
                return TaskKind.CutTrees;
        }

        return null;
    }

    private static bool IsReadyToHarvest(Crop crop) =>
        crop.currentPhase.Value >= crop.phaseDays.Count - 1 && !crop.dead.Value;
}
