using Dayswork.Core.Domain;
using Dayswork.Core.Energy;
using Dayswork.Core.Inventory;
using Dayswork.Core.Machines;
using Dayswork.Core.Shifts;
using Dayswork.Worker;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData.Machines;
using StardewValley.Objects;
using SObject = StardewValley.Object;

namespace Dayswork.Orchestration;

/// <summary>
/// Manage Machines execution. A machine batch (one per location with selected machines) visits each
/// machine group-major: collect ready output into the deposit buffer, then — for collect-and-reload
/// groups — fetch input from the group's chest (physically walking to it) and load the empty
/// machines. Mirrors the managed-crop batch lifecycle.
///
/// v1 scope: the input fetch is a within-location walk. A group whose input chest is in a different
/// location than its machines is serviced collect-only for that location (a HUD/dev note), so the
/// inputs are never touched and never lost — full cross-location fetch trips are a follow-up.
/// </summary>
internal sealed partial class ShiftOrchestrator
{
    private readonly MachineReader _machineReader = new();

    private static bool IsMachineBatch(WorkBatch batch) => batch.Kind == BatchKind.Machines;

    private void ResetMachineState()
    {
        if (_session is not { } s)
            return;

        s.MachineSteps.Clear();
        s.CurrentMachineStep = null;
        s.MachinesActive = false;
        s.MachineBatchLocationName = "Farm";
        s.MachineReloads.Clear();
        s.CurrentMachineReload = null;
        s.MachineFetchPending = false;
        s.CarriedInputs.Clear();
        s.CarriedInputsChest = null;
    }

    private static GameLocation? ResolveMachineBatchLocation(string locationName) =>
        string.Equals(locationName, "Farm", StringComparison.Ordinal)
            ? Game1.getFarm()
            : Game1.getLocationFromName(locationName);

    private void BeginMachineBatch(WorkBatch batch)
    {
        if (_session is null || Session.Worker is null)
            return;

        ResetMachineState();
        Session.MachineBatchLocationName = batch.LocationName;

        var location = ResolveMachineBatchLocation(batch.LocationName);
        var scope = Session.Ctx.WorkScopes.Machines;
        if (location is null || scope is null)
        {
            DevLog.Log($"[Dayswork][machines] skipped batch={batch.LocationName} reason=location_or_scope_unavailable.", DevLog.WarnLevel);
            Session.Ctx.CurrentBatchIndex++;
            BeginCurrentBatch();
            return;
        }

        Session.CurrentLocation = location;

        var groups = scope.Groups
            .Where(group => group.Machines.Any(machine =>
                string.Equals(machine.LocationName, batch.LocationName, StringComparison.Ordinal)))
            .ToList();

        foreach (var group in groups)
            PlanMachineGroup(group, batch.LocationName, location);

        ModEntry.ModMonitor.Log(
            $"[Dayswork][machines] batch={batch.LocationName} groups={groups.Count} collectSteps={Session.MachineSteps.Count} reloadJobs={Session.MachineReloads.Count}.",
            DevLog.WarnLevel);

        if (Session.MachineSteps.Count == 0 && Session.MachineReloads.Count == 0)
        {
            ModEntry.ModMonitor.Log(
                $"[Dayswork][machines] batch={batch.LocationName} — no machines ready to collect or reload; skipping.",
                DevLog.WarnLevel);
            Session.Ctx.CurrentBatchIndex++;
            BeginCurrentBatch();
            return;
        }

        Session.MachinesActive = true;
        StartNextMachineStep();
    }

    private void PlanMachineGroup(MachineGroup group, string batchLocationName, GameLocation location)
    {
        if (_session is null)
            return;

        var groupMachinesHere = group.Machines
            .Where(machine => string.Equals(machine.LocationName, batchLocationName, StringComparison.Ordinal))
            .ToList();

        var reloadable = new List<(MachineRef Ref, SObject Machine, MachineData Data)>();
        foreach (var machineRef in groupMachinesHere)
        {
            var live = _machineReader.Resolve(location, machineRef.Tile, machineRef.ExpectedQualifiedId);
            if (live is null)
            {
                DevLog.Log($"[Dayswork][machines] skip machine ({machineRef.Tile.X},{machineRef.Tile.Y}) — not the selected machine anymore.", DevLog.WarnLevel);
                continue;
            }

            var state = _machineReader.Classify(live);

            // A machine wants reloading when its group is in reload mode and the machine type is
            // reloadable. A ReadyToCollect machine qualifies too — it becomes Empty after its Collect
            // step runs, so we plan its reload now (the Load step is guarded on Empty, so it only
            // fires once the collect has emptied it).
            var wantsReload = group.Mode == MachineGroupMode.CollectAndReload
                && live.GetMachineData() is { } data
                && MachineReader.IsReloadable(data);

            if (state == MachineReadyState.ReadyToCollect)
            {
                Session.MachineSteps.Enqueue(new MachineStep(machineRef, MachineActionKind.Collect, group, null));
                if (wantsReload && live.GetMachineData() is { } collectData)
                    reloadable.Add((machineRef, live, collectData));
            }
            else if (state == MachineReadyState.Empty && wantsReload && live.GetMachineData() is { } emptyData)
            {
                reloadable.Add((machineRef, live, emptyData));
            }
            else
            {
                DevLog.Log(
                    $"[Dayswork][machines] skip ({machineRef.Tile.X},{machineRef.Tile.Y}) state={state} mode={group.Mode}.",
                    LogLevel.Debug);
            }
        }

        if (group.Mode != MachineGroupMode.CollectAndReload || group.InputChest is not { } chestRef || reloadable.Count == 0)
            return;

        if (!string.Equals(chestRef.LocationName, location.NameOrUniqueName, StringComparison.Ordinal))
        {
            // v1: physical fetch is within-location only; a chest elsewhere ⇒ collect-only here.
            ModEntry.ModMonitor.Log(
                $"[Dayswork][machines] group '{group.Id}' input chest is in '{chestRef.LocationName}', not '{location.NameOrUniqueName}'; reload skipped this location (collect-only).",
                DevLog.WarnLevel);
            return;
        }

        var supply = ReadChestSupply(chestRef);
        var probeFarmer = CreateWorkerActionFarmer(reloadable[0].Ref.Tile, location);
        var candidates = new List<MachineLoadCandidate>();
        foreach (var (machineRef, machine, data) in reloadable)
        {
            var candidate = _machineReader.BuildLoadCandidate(machineRef, machine, data, group.InputFilter, supply, probeFarmer, location);
            if (candidate is not null)
                candidates.Add(candidate);
        }

        if (candidates.Count == 0)
        {
            ModEntry.ModMonitor.Log(
                $"[Dayswork][machines] group '{group.Id}': {reloadable.Count} reloadable machine(s) but no matching inputs in chest — check the input chest contents.",
                DevLog.WarnLevel);
            return;
        }

        var plan = MachineInputPlanner.Plan(candidates, supply);
        if (plan.HasWork)
            Session.MachineReloads.Enqueue(new MachineReloadJob(group, chestRef, plan));
        else
            ModEntry.ModMonitor.Log(
                $"[Dayswork][machines] group '{group.Id}': {candidates.Count} matchable machine(s) but chest supply insufficient to fill any.",
                DevLog.WarnLevel);
    }

    private void StartNextMachineStep()
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

        var location = Session.CurrentLocation ?? ResolveMachineBatchLocation(Session.MachineBatchLocationName) ?? Game1.getFarm();

        while (Session.MachineSteps.Count > 0)
        {
            var step = Session.MachineSteps.Dequeue();
            var live = _machineReader.Resolve(location, step.Machine.Tile, step.Machine.ExpectedQualifiedId);
            if (live is null)
                continue;

            var state = _machineReader.Classify(live);
            if (step.Kind == MachineActionKind.Collect && state != MachineReadyState.ReadyToCollect)
                continue;
            if (step.Kind == MachineActionKind.Load && state != MachineReadyState.Empty)
                continue;

            Session.CurrentMachineStep = step;
            var navTile = ResolveMachineNavTile(step.Machine.Tile, location);
            Session.PendingNavTile = navTile;
            Session.PendingTaskTile = step.Machine.Tile;
            EnsureWorkingIntent(new IntentMoveToTile(navTile));
            _nav.StartNavigation(navTile, location, Session.Worker);
            return;
        }

        AdvanceMachineReload();
    }

    private void AdvanceMachineReload()
    {
        if (_session is null || Session.Worker is null)
            return;

        // Return any inputs left over from the just-finished job before starting the next.
        SettleCarriedInputs();
        Session.CurrentMachineReload = null;

        if (ShouldWrapUpBeforeNextUnit())
        {
            QueueWrapUpNow(Session.Ctx.PendingStopReason ?? ShiftStopReason.Exhausted);
            return;
        }

        if (Session.MachineReloads.Count == 0)
        {
            CompleteMachineBatch();
            return;
        }

        var job = Session.MachineReloads.Dequeue();
        Session.CurrentMachineReload = job;

        var location = Session.CurrentLocation ?? ResolveMachineBatchLocation(Session.MachineBatchLocationName) ?? Game1.getFarm();
        if (!ShiftOrchestrator.TrySelectChestDepositStandTile(job.Chest.Tile, location, Session.Worker, out var standTile))
        {
            DevLog.Log($"[Dayswork][machines] cannot reach input chest ({job.Chest.Tile.X},{job.Chest.Tile.Y}); reload skipped.", DevLog.WarnLevel);
            AdvanceMachineReload();
            return;
        }

        Session.MachineFetchPending = true;
        Session.PendingNavTile = standTile;
        Session.PendingTaskTile = job.Chest.Tile;
        EnsureWorkingIntent(new IntentMoveToTile(standTile));
        _nav.StartNavigation(standTile, location, Session.Worker);
    }

    private void OnMachineFetchArrived()
    {
        if (_session is null)
            return;

        Session.MachineFetchPending = false;
        var job = Session.CurrentMachineReload;
        if (job is null)
        {
            StartNextMachineStep();
            return;
        }

        WithdrawInputs(job);

        foreach (var assignment in job.Plan.Assignments)
            Session.MachineSteps.Enqueue(new MachineStep(assignment.Machine, MachineActionKind.Load, job.Group, assignment.Requirement));

        StartNextMachineStep();
    }

    private void HandleMachineAction(IntentPerformMachineAction intent, GameLocation location)
    {
        if (_session is null || Session.Worker is null)
            return;

        var step = Session.CurrentMachineStep;
        if (step is null)
        {
            StartNextMachineStep();
            return;
        }

        if (!Session.ActionPending)
        {
            _toolAnimator.StopSwing();
            _toolAnimator.PlaySwing(WorkerTool.None, FacingToward(Session.Worker.TilePoint, step.Machine.Tile, Session.Worker.FacingDirection));

            var live = _machineReader.Resolve(location, step.Machine.Tile, step.Machine.ExpectedQualifiedId);
            if (live is not null)
            {
                if (step.Kind == MachineActionKind.Collect)
                {
                    CollectMachine(live, step, location);
                    SpendStaminaForBeat(WorkActionKind.CollectMachine);
                }
                else
                {
                    LoadMachine(live, step, location);
                    SpendStaminaForBeat(WorkActionKind.LoadMachine);
                }
            }

            Session.ActionPending = true;
            return;
        }

        if (_toolAnimator.IsSwinging)
            return;

        Session.ActionPending = false;
        Session.CurrentMachineStep = null;

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
        StartNextMachineStep();
    }

    private void CollectMachine(SObject machine, MachineStep step, GameLocation location)
    {
        RunMachineActionGuarded(() =>
        {
            var output = machine.heldObject.Value;
            if (output is null || !machine.readyForHarvest.Value)
                return;

            var qualifiedId = output.QualifiedItemId;
            var stack = Math.Max(1, output.Stack);

            var who = CreateWorkerActionFarmer(step.Machine.Tile, location);
            bool cleared;
            try
            {
                if (Game1.player.currentLocation == location)
                {
                    // Player is here — use checkForAction for correct RecalculateOnCollect flavor
                    // (e.g. bee houses re-derive honey at collect time). Note: checkForAction's own
                    // "coin" sound is gated behind who.IsLocalPlayer, which is false for the fake
                    // worker farmer — so it stays silent here and we emit the sound explicitly below.
                    machine.checkForAction(who);
                    cleared = machine.heldObject.Value is null || !machine.readyForHarvest.Value;
                }
                else
                {
                    // Player is elsewhere — checkForAction has no playSounds:false equivalent, so bypass
                    // it to avoid cross-location sounds. Give the held output to the fake farmer directly
                    // and clear the machine state manually. RecalculateOnCollect machines (e.g. bee
                    // houses) will use the stored output rather than re-deriving flavor at collection
                    // time, which is acceptable when the player isn't present.
                    who.addItemToInventory(output.getOne());
                    machine.heldObject.Value = null;
                    machine.readyForHarvest.Value = false;
                    machine.showNextIndex.Value = false;
                    machine.ResetParentSheetIndex();

                    // Re-fire the OutputCollected rule so chained-output machines keep producing —
                    // most importantly the Crystalarium, which re-derives the next gem from its
                    // remembered input (machine.lastInputItem), not a fresh chest input. This mirrors
                    // vanilla CheckForActionOnMachine; without it the machine is left empty (the bug:
                    // the loaded mineral vanishes). The re-trigger is silent off-location — OutputMachine
                    // sets a future ready time (no "dwop") and addWorkingAnimation early-returns when
                    // no farmer is in the location. Machines with no OutputCollected rule (furnace,
                    // keg, …) match no rule here, so heldObject stays null exactly as before.
                    var machineData = machine.GetMachineData();
                    if (machineData is not null
                        && MachineDataUtility.TryGetMachineOutputRule(
                            machine, machineData, MachineOutputTrigger.OutputCollected, output.getOne(),
                            who, location, out var collectRule, out _, out _, out _))
                    {
                        machine.OutputMachine(machineData, collectRule, machine.lastInputItem.Value, who, location, probe: false);
                    }
                    cleared = true;
                }
            }
            catch (Exception ex)
            {
                ModEntry.ModMonitor.Log($"[Dayswork][machines] collect threw at ({step.Machine.Tile.X},{step.Machine.Tile.Y}): {ex.Message}", DevLog.WarnLevel);
                return;
            }

            // Only credit the buffer if the machine actually released its output (duplication-safe:
            // if checkForAction couldn't collect, the item is still in the world for the player).
            if (!cleared || string.IsNullOrWhiteSpace(qualifiedId))
            {
                DevLog.Log($"[Dayswork][machines] collect at ({step.Machine.Tile.X},{step.Machine.Tile.Y}) left output in place; not credited.", DevLog.WarnLevel);
                return;
            }

            // Capture flavored/colored identity (blueberry wine, aged roe, flavored honey…) and the
            // output's quality (cask-aged wine/cheese can be silver/gold/iridium) so the deposit
            // pipeline rebuilds the exact item. Prefer the item the worker actually collected (correct
            // for RecalculateOnCollect machines like bee houses), else the held output.
            var collected = who.Items.FirstOrDefault(i =>
                i is not null && string.Equals(i.QualifiedItemId, qualifiedId, StringComparison.Ordinal)) ?? output;
            var flavorId = Session.Flavors.Register(collected);
            var quality = (collected as SObject)?.Quality ?? output.Quality;

            Session.Ctx.Buffer.Add(qualifiedId, stack, TaskKind.HarvestCrops, MachineOutputRouter.ProvenanceFor(step.Group), quality: quality, flavorId: flavorId);

            // Vanilla plays "coin" on machine collect, but only for the local player (see comment
            // above) — emit it ourselves, gated on the player being here so off-farm machine work
            // stays silent (matches every other worker action).
            if (Game1.player.currentLocation == location)
                location.playSound("coin", new Vector2(step.Machine.Tile.X, step.Machine.Tile.Y));
        });
    }

    private void LoadMachine(SObject machine, MachineStep step, GameLocation location)
    {
        if (_session is null || step.Load is not { } recipe)
            return;

        var demand = recipe.TotalDemand();
        foreach (var (id, need) in demand)
        {
            if (Session.CarriedInputs.GetValueOrDefault(id) < need)
            {
                DevLog.Log($"[Dayswork][machines] load skipped at ({step.Machine.Tile.X},{step.Machine.Tile.Y}) — carry buffer short of {id}.", DevLog.WarnLevel);
                return;
            }
        }

        RunMachineActionGuarded(() =>
        {
            var data = machine.GetMachineData();
            if (data is null)
                return;

            var who = CreateWorkerActionFarmer(step.Machine.Tile, location);
            foreach (var (id, need) in demand)
            {
                var item = ItemRegistry.Create(id, need, allowNull: true);
                if (item is not null)
                    who.addItemToInventory(item);
            }

            var inputItem = who.Items.FirstOrDefault(i =>
                i is not null && string.Equals(i.QualifiedItemId, recipe.InputQualifiedId, StringComparison.Ordinal));
            if (inputItem is null)
                return;

            bool loaded;
            try
            {
                // Let vanilla emit the machine-specific load sound (PlayEffects, not IsLocalPlayer-gated)
                // only while the player is in this location — silent for off-farm machine work.
                loaded = machine.PlaceInMachine(data, inputItem, probe: false, who, showMessages: false, playSounds: Game1.player.currentLocation == location);
            }
            catch (Exception ex)
            {
                ModEntry.ModMonitor.Log($"[Dayswork][machines] load threw at ({step.Machine.Tile.X},{step.Machine.Tile.Y}): {ex.Message}", DevLog.WarnLevel);
                return;
            }

            if (!loaded)
                return;

            // Consumed = what we gave minus what the fake farmer has left; subtract from the carry buffer.
            foreach (var (id, need) in demand)
            {
                var remaining = who.Items
                    .Where(i => i is not null && string.Equals(i.QualifiedItemId, id, StringComparison.Ordinal))
                    .Sum(i => i.Stack);
                var consumed = Math.Max(0, need - remaining);
                Session.CarriedInputs[id] = Math.Max(0, Session.CarriedInputs.GetValueOrDefault(id) - consumed);
            }
        });
    }

    private void RunMachineActionGuarded(Action body)
    {
        var playerState = new Game1WorkerActionPlayerState(Game1.player);
        var savedState = WorkerActionPlayerStateSnapshot.Capture(playerState);
        var hudMessageCountBefore = Game1.hudMessages.Count;

        try
        {
            body();
        }
        finally
        {
            savedState.Restore(playerState);
            while (Game1.hudMessages.Count > hudMessageCountBefore)
                Game1.hudMessages.RemoveAt(Game1.hudMessages.Count - 1);
        }
    }

    private IReadOnlyDictionary<string, int> ReadChestSupply(ChestRef chestRef)
    {
        var chest = _chestResolver.ResolveChest(chestRef);
        var supply = new Dictionary<string, int>(StringComparer.Ordinal);
        if (chest is null)
            return supply;

        foreach (var item in chest.Items)
            if (item is not null && !string.IsNullOrWhiteSpace(item.QualifiedItemId))
                supply[item.QualifiedItemId] = supply.GetValueOrDefault(item.QualifiedItemId) + item.Stack;

        return supply;
    }

    private void WithdrawInputs(MachineReloadJob job)
    {
        if (_session is null)
            return;

        var chest = _chestResolver.ResolveChest(job.Chest);
        if (chest is null || chest.GetMutex().IsLocked())
        {
            DevLog.Log($"[Dayswork][machines] input chest missing/busy at ({job.Chest.Tile.X},{job.Chest.Tile.Y}); reload skipped.", DevLog.WarnLevel);
            return;
        }

        Session.CarriedInputsChest = job.Chest;

        // The worker pulls inputs directly from the chest's item list (no ItemGrabMenu), so the
        // vanilla open + per-item pickup sounds never fire. Emit them ourselves, mirroring the
        // deposit trip's chest audio: "openChest" on open, then the inventory move cue "dwop" for
        // each item type taken — staggered, since the withdrawal is synchronous and same-frame cues
        // would overlap into one. Gated on the player being in the chest's location so off-farm
        // fetches stay silent (matches every other worker action).
        var chestLoc = chest.Location;
        var playerHere = chestLoc is not null && Game1.player.currentLocation == chestLoc;
        var chestTile = new Vector2(chest.TileLocation.X, chest.TileLocation.Y);
        if (playerHere)
        {
            chest.frameCounter.Value = 5;  // vanilla open trigger (matches DepositTripRunner)
            chestLoc!.playSound("openChest", chestTile);
        }

        var takeSoundIndex = 0;
        foreach (var (id, want) in job.Plan.Withdrawals)
        {
            var taken = RemoveFromChest(chest, id, want);
            if (taken <= 0)
                continue;

            Session.CarriedInputs[id] = Session.CarriedInputs.GetValueOrDefault(id) + taken;
            if (playerHere)
                DelayedAction.playSoundAfterDelay("dwop", 150 + takeSoundIndex * 90, chestLoc, chestTile);
            takeSoundIndex++;
        }
    }

    private static int RemoveFromChest(Chest chest, string qualifiedId, int amount)
    {
        var remaining = amount;
        for (var i = chest.Items.Count - 1; i >= 0 && remaining > 0; i--)
        {
            var item = chest.Items[i];
            if (item is null || !string.Equals(item.QualifiedItemId, qualifiedId, StringComparison.Ordinal))
                continue;

            var take = Math.Min(item.Stack, remaining);
            item.Stack -= take;
            remaining -= take;
            if (item.Stack <= 0)
                chest.Items.RemoveAt(i);
        }

        return amount - remaining;
    }

    // Settles any inputs still in the carry buffer back into their source chest (mutex-checked), or
    // to automatic overflow if the chest is gone/busy/full. Inputs are never lost (hard rule 4).
    private void SettleCarriedInputs()
    {
        if (_session is null || Session.CarriedInputs.Count == 0)
            return;

        var chest = Session.CarriedInputsChest is { } chestRef ? _chestResolver.ResolveChest(chestRef) : null;
        var chestWritable = chest is not null && !chest.GetMutex().IsLocked();

        // Returning leftover inputs is the inverse of WithdrawInputs and writes the chest directly,
        // so emit the same hand-rolled chest audio (the menu sounds never fire): "openChest" once,
        // then "dwop" per item type actually returned (staggered, since this is synchronous). Gated
        // on the player being in the chest's location so off-farm settles stay silent.
        var chestLoc = chestWritable ? chest!.Location : null;
        var playerHere = chestLoc is not null && Game1.player.currentLocation == chestLoc;
        var chestTile = chest is not null ? new Vector2(chest.TileLocation.X, chest.TileLocation.Y) : Vector2.Zero;
        var chestOpened = false;
        var returnSoundIndex = 0;

        foreach (var (id, count) in Session.CarriedInputs.ToList())
        {
            if (count <= 0)
                continue;

            var item = ItemRegistry.Create(id, count, allowNull: true);
            if (item is null)
                continue;

            Item? leftover = item;
            if (chestWritable)
                leftover = chest!.addItem(item);

            // At least part of the stack landed in the chest → play the return audio.
            if (playerHere && (leftover is null || leftover.Stack < count))
            {
                if (!chestOpened)
                {
                    chest!.frameCounter.Value = 5;  // vanilla open trigger (matches WithdrawInputs)
                    chestLoc!.playSound("openChest", chestTile);
                    chestOpened = true;
                }
                DelayedAction.playSoundAfterDelay("dwop", 150 + returnSoundIndex * 90, chestLoc, chestTile);
                returnSoundIndex++;
            }

            if (leftover is not null && leftover.Stack > 0)
                Session.Ctx.Overflow.Add(new OverflowItem(
                    new RoutedItemStack(id, leftover.Stack, TaskKind.HarvestCrops, OutputScopeProvenance.Unknown),
                    OverflowReason.NotDelivered));
        }

        Session.CarriedInputs.Clear();
        Session.CarriedInputsChest = null;
    }

    private TileCoord ResolveMachineNavTile(TileCoord machineTile, GameLocation location)
    {
        if (Session.Worker is not null
            && ShiftOrchestrator.TrySelectChestDepositStandTile(machineTile, location, Session.Worker, out var stand))
            return stand;

        return new TileCoord(machineTile.X, machineTile.Y + 1);
    }

    private void CompleteMachineBatch()
    {
        if (_session is null)
            return;

        SettleCarriedInputs();
        var completedLocation = Session.MachineBatchLocationName;
        ResetMachineState();

        if (TryStartMachineBatchExitTravel(completedLocation))
            return;

        Session.Ctx.CurrentBatchIndex++;
        BeginCurrentBatch();
    }

    private bool TryStartMachineBatchExitTravel(string locationName)
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

/// <summary>One machine visit: collect ready output, or load an empty machine with the chosen recipe.</summary>
internal sealed record MachineStep(
    MachineRef Machine,
    MachineActionKind Kind,
    MachineGroup Group,
    RecipeRequirement? Load);

/// <summary>A pending reload for one group in the current batch: fetch from the chest, then load.</summary>
internal sealed record MachineReloadJob(
    MachineGroup Group,
    ChestRef Chest,
    MachineLoadPlan Plan);
