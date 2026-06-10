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

internal sealed partial class ShiftOrchestrator
{
    private void HandleTaskAction(IntentPerformTaskAt intent, GameLocation location)
    {
        // Don't start a new swing while a felled tree is still falling — the trunk's fall
        // animation must finish before the stump is choppable. Swinging now wastes energy
        // and plays a phantom chop (most visible at fast animation speeds). The deferred
        // beat keeps the worker idle until falling clears, then chopping resumes.
        if (!Session.ActionPending
            && intent.Task == TaskKind.CutTrees
            && IsCutTreeTargetFalling(intent.Tile, location))
            return;

        if (!Session.ActionPending)
        {
            _toolAnimator.StopSwing();
            _toolAnimator.PlaySwing(intent.Task, FacingToward(Session.Worker!.TilePoint, intent.Tile, Session.Worker.FacingDirection));
//             ModEntry.ModMonitor.Log(
//                 $"[Dayswork][action] invoke task={intent.Task} taskTile=({intent.Tile.X},{intent.Tile.Y}) worker=({Session.Worker.TilePoint.X},{Session.Worker.TilePoint.Y}).",
//                 LogLevel.Trace);
            Session.PendingBeatOutcome = InvokeTaskActionGuarded(intent.Tile, intent.Task, location);
            SpendStaminaForBeat(ActionKindForTask(intent.Task));
            Session.ActionPending = true;
            return;
        }

        if (_toolAnimator.IsSwinging)
            return;

        var outcome = Session.PendingBeatOutcome ?? new LaborBeatOutcome(true, IsTaskComplete(intent.Tile, intent.Task, location));
        Session.PendingBeatOutcome = null;
        Session.ActionPending = false;

        var boundary = _boundaryClassifier.EvaluateAfterBeat(
            outcome.UnitResolved,
            Session.Ctx.EnergyState,
            HasBoundaryStopRequested());

        if (boundary.CanContinueCurrentUnit)
            return;

        if (boundary.ShouldWrapUpAfterCurrentUnit)
        {
            QueueWrapUpNow(Session.Ctx.PendingStopReason ?? ShiftStopReason.Exhausted);
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
        var location = Session.CurrentLocation ?? Game1.getFarm();
        var animal = _animalHandler.FindLiveAnimal(location, intent.Animal);
        if (animal is null)
        {
            Session.ActionPending = false;
            Session.CurrentAnimalWork = null;
            FinishResolvedAnimalWork(location, madeProgress: false);
            return;
        }

        if (!Session.ActionPending)
        {
            _toolAnimator.StopSwing();
            _toolAnimator.PlaySwing(WorkerTool.None,
                FacingToward(Session.Worker!.TilePoint, _animalHandler.CurrentTile(animal), Session.Worker.FacingDirection));
            SpendStaminaForBeat(WorkActionKind.PetAnimal);
            Session.ActionPending = true;
            return;
        }

        if (_toolAnimator.IsSwinging)
            return;

        _animalHandler.Pet(animal);
        Session.ActionPending = false;
        Session.CurrentAnimalWork = null;
        FinishResolvedAnimalWork(location);
    }

    private void HandleCollectFromAnimal(IntentCollectFromAnimal intent)
    {
        var location = Session.CurrentLocation ?? Game1.getFarm();
        var animal = _animalHandler.FindLiveAnimal(location, intent.Animal);
        if (animal is null)
        {
            Session.ActionPending = false;
            Session.CurrentAnimalWork = null;
            FinishResolvedAnimalWork(location, madeProgress: false);
            return;
        }

        if (!Session.ActionPending)
        {
            _toolAnimator.StopSwing();
            var collectTool = AnimalTaskHandler.IsShearProduce(animal) ? WorkerTool.Shears
                            : AnimalTaskHandler.IsMilkProduce(animal)  ? WorkerTool.MilkPail
                            : WorkerTool.None;
            _toolAnimator.PlaySwing(collectTool,
                FacingToward(Session.Worker!.TilePoint, _animalHandler.CurrentTile(animal), Session.Worker.FacingDirection));
            PlayAnimalCollectSound(location, collectTool);
            SpendStaminaForBeat(WorkActionKind.CollectAnimalProduct);
            Session.ActionPending = true;
            return;
        }

        if (_toolAnimator.IsSwinging)
            return;

        _animalHandler.TryCollect(animal, Session.Ctx.Buffer, Session.CurrentAnimalWork?.Provenance ?? Session.PendingOutputProvenance);
        Session.ActionPending = false;
        Session.CurrentAnimalWork = null;
        FinishResolvedAnimalWork(location);
    }

    private static void PlayAnimalCollectSound(GameLocation location, WorkerTool collectTool)
    {
        location.playSound(AnimalCollectAudioCue.ForTool(collectTool));
    }

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
        // be removed below. The worker's items end up in Session.Ctx.Buffer, never the player's
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
        DevLog.Log(
            $"[Dayswork][player-action-guard] Worker task {task} at ({tile.X},{tile.Y}) in {location.NameOrUniqueName} changed Game1.player action state while playerTool={DescribePlayerTool(Game1.player)}; restored. saved={{ {savedState} }} changed={{ {changedState} }} restored={{ {restoredState} }}");
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
                Session.Ctx.Buffer.Add(item.QualifiedItemId, item.Stack, TaskKind.HarvestCrops, Session.PendingOutputProvenance);
                Game1.player.removeItemFromInventory(item);
            }
            else if (item.Stack > oldStack)
            {
                // Existing slot grew (stacked onto items the player already had).
                var gain = item.Stack - oldStack;
                Session.Ctx.Buffer.Add(item.QualifiedItemId, gain, TaskKind.HarvestCrops, Session.PendingOutputProvenance);
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
        CollectNewDebrisAtTile(before, loc, Session.PendingTask, tileVec, Session.PendingOutputProvenance);
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
        CollectNewDebrisAtTile(before, loc, Session.PendingTask, tileVec, Session.PendingOutputProvenance);
        // Shaken fruit settles over the next several beats, so an immediate sweep misses it.
        // Queue a delayed sweep (same mechanism trees use for falling wood) to catch it.
        if (hadFruit)
            QueueDelayedDebrisSweep(loc, tileVec, before, Session.PendingTask, Session.PendingOutputProvenance);
        return new LaborBeatOutcome(true, true);
    }

    private LaborBeatOutcome InvokeCollectAnimalProduct(TileCoord tile, GameLocation loc)
    {
        var tileVec = new Vector2(tile.X, tile.Y);
        if (!loc.objects.TryGetValue(tileVec, out var obj) ||
            !WorkAreaScanner.IsAnimalProductForageObject(obj))
            return new LaborBeatOutcome(true, true);

        Session.Ctx.Buffer.Add(obj.QualifiedItemId, Math.Max(1, obj.Stack), TaskKind.CollectAnimalProducts, Session.PendingOutputProvenance);
        loc.removeObject(tileVec, false);
        return new LaborBeatOutcome(true, true);
    }

    private LaborBeatOutcome InvokeFeedAnimal(TileCoord tile, GameLocation loc)
    {
        if (Session.CurrentFeedPlan is null)
            return new LaborBeatOutcome(true, true);

        if (tile == Session.CurrentFeedPlan.HopperTile && Session.HayInHand <= 0)
        {
            if (_animalHandler.TakeHay(loc, Session.CurrentFeedPlan.HayToTake))
                Session.HayInHand = Session.CurrentFeedPlan.HayToTake;
            return new LaborBeatOutcome(true, true);
        }

        if (Session.HayInHand <= 0)
            return new LaborBeatOutcome(true, true);

        if (_animalHandler.PlaceHay(loc, tile))
            Session.HayInHand--;
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
        CollectNewDebrisAtTile(before, loc, Session.PendingTask, tileVec, Session.PendingOutputProvenance);
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
        var pickaxe = new Pickaxe { UpgradeLevel = (int)Session.Ctx.ToolSnapshot.PickaxeLevel, lastUser = CreateWorkerActionFarmer(tile, loc) };

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
            CollectNewDebrisAtTile(beforeClump, loc, Session.PendingTask, clump.Tile, Session.PendingOutputProvenance);

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
        var collectedDebris = CollectNewDebrisAtTile(before, loc, Session.PendingTask, tileVec, Session.PendingOutputProvenance);
        if (!collectedDebris && removed && TryGetRemovedStandardStoneDrop(obj, out var itemId, out var stack))
        {
            Session.Ctx.Buffer.Add(itemId, stack, Session.PendingTask, Session.PendingOutputProvenance);
//             ModEntry.ModMonitor.Log(
//                 $"[Dayswork][debris] collected {stack}x {itemId} from removed standard stone object task={Session.PendingTask}.",
//                 LogLevel.Trace);
        }

        return new LaborBeatOutcome(true, true);
    }

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
        var axe = new Axe { UpgradeLevel = (int)Session.Ctx.ToolSnapshot.AxeLevel, lastUser = CreateWorkerActionFarmer(tile, loc) };

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
            CollectNewDebrisAtTile(before, loc, Session.PendingTask, tileVec, Session.PendingOutputProvenance);
            if (!wasStump && !removeTree)
                QueueDelayedDebrisSweep(loc, tileVec, before, Session.PendingTask, Session.PendingOutputProvenance);

            if (!wasStump && tree.stump.Value)
                return new LaborBeatOutcome(true, false);

            return new LaborBeatOutcome(removeTree, removeTree);
        }

        if (ObjectTargetClassifier.FindResourceClumpAt(tileVec, loc) is { } clump)
        {
            var beforeClump = new HashSet<Debris>(loc.debris);
            var destroyed   = clump.performToolAction(axe, 0, clump.Tile);
            CollectNewDebrisAtTile(beforeClump, loc, Session.PendingTask, clump.Tile, Session.PendingOutputProvenance);
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
            CollectNewDebrisAtTile(before, loc, Session.PendingTask, tileVec, Session.PendingOutputProvenance);
            return new LaborBeatOutcome(true, true);
        }

        return new LaborBeatOutcome(true, true);
    }

    private Farmer CreateWorkerActionFarmer(TileCoord taskTile, GameLocation location)
    {
        var actionFarmer = Game1.player.CreateFakeEventFarmer();
        actionFarmer.currentLocation = location;
        actionFarmer.Position = Session.Worker?.Position ?? Game1.player.Position;
        actionFarmer.faceDirection(
            FacingToward(
                Session.Worker?.TilePoint ?? Game1.player.TilePoint,
                taskTile,
                Session.Worker?.FacingDirection ?? Game1.player.FacingDirection));
        actionFarmer.CanMove = false;
        actionFarmer.UsingTool = false;
        actionFarmer.canReleaseTool = false;
        actionFarmer.jitterStrength = 0f;
        actionFarmer.FarmerSprite.pauseForSingleAnimation = false;
        actionFarmer.FarmerSprite.StopAnimation();
        return actionFarmer;
    }

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
}
