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
/// Manage Machines execution. A machine batch (one per location with selected machines) services its
/// groups one at a time, running a full collect→reload cycle per group before moving to the next. For
/// a collect-and-reload group the worker first walks to that group's input chest and withdraws the
/// planned inputs (one trip), then visits each machine exactly once — collecting its ready output into
/// the deposit buffer and immediately reloading the now-empty machine — before advancing to the next
/// group. Collect-only groups skip the chest trip. Mirrors the managed-crop batch lifecycle.
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
        s.PendingMachineGroups.Clear();
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
            CompleteMachineBatch();
            return;
        }

        Session.CurrentLocation = location;

        var groups = scope.Groups
            .Where(group => group.Machines.Any(machine =>
                string.Equals(machine.LocationName, batch.LocationName, StringComparison.Ordinal)))
            .ToList();

        foreach (var group in groups)
            Session.PendingMachineGroups.Enqueue(group);

        DevLog.Log(
            $"[Dayswork][machines] batch={batch.LocationName} groups={groups.Count} (per-group collect→reload).",
            DevLog.WarnLevel);

        // Each group is planned lazily when it comes up (see AdvanceMachineGroup) so its collect and
        // reload run as one cycle before the next group. An all-empty batch falls through to
        // CompleteMachineBatch on the first AdvanceMachineGroup; no game tick elapses before then, so
        // setting MachinesActive here is safe even on the skip path.
        Session.MachinesActive = true;
        AdvanceMachineGroup();
    }

    /// <summary>
    /// Begins the next group's collect→reload cycle. Plans just that group (so each group is read
    /// against fresh live state and a shared input chest reflects earlier groups' withdrawals), runs
    /// its steps, and only advances to the following group once this one's reload settles. When no
    /// groups remain, completes the batch. Groups that plan to nothing are skipped.
    /// </summary>
    private void AdvanceMachineGroup()
    {
        if (_session is null || Session.Worker is null)
            return;

        if (ShouldWrapUpBeforeNextUnit())
        {
            QueueWrapUpNow(Session.Ctx.PendingStopReason ?? ShiftStopReason.Exhausted);
            return;
        }

        if (Session.PendingMachineGroups.Count == 0)
        {
            CompleteMachineBatch();
            return;
        }

        var group = Session.PendingMachineGroups.Dequeue();
        var location = Session.CurrentLocation
            ?? ResolveMachineBatchLocation(Session.MachineBatchLocationName) ?? Game1.getFarm();

        PlanMachineGroup(group, Session.MachineBatchLocationName, location);

        // Group planned to nothing (busy / collect-only with no output / no matching inputs) → next.
        if (Session.MachineSteps.Count == 0 && Session.MachineReloads.Count == 0)
        {
            AdvanceMachineGroup();
            return;
        }

        // Fetch this group's inputs FIRST (one chest trip), then visit each machine once to
        // collect→reload (the collect/load steps are already queued interleaved per machine).
        // Collect-only groups (no reload job) skip the fetch and go straight to their collect steps.
        if (Session.MachineReloads.Count > 0)
            AdvanceMachineReload();
        else
            StartNextMachineStep();
    }

    private void PlanMachineGroup(MachineGroup group, string batchLocationName, GameLocation location)
    {
        if (_session is null)
            return;

        var groupMachinesHere = group.Machines
            .Where(machine => string.Equals(machine.LocationName, batchLocationName, StringComparison.Ordinal))
            .ToList();

        var readyToCollect = new HashSet<TileCoord>();
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
            // step runs in the same visit (the Load step is guarded on Empty, so it only fires once
            // the collect has emptied it).
            var wantsReload = group.Mode == MachineGroupMode.CollectAndReload
                && live.GetMachineData() is { } data
                && MachineReader.IsReloadable(data);

            if (state == MachineReadyState.ReadyToCollect)
            {
                readyToCollect.Add(machineRef.Tile);
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

        // Build the load plan (assignments + withdrawals) BEFORE queuing any steps, so each machine's
        // collect and reload can be queued back-to-back as one visit and the chest fetch can run
        // first (one trip, each machine visited once).
        var (plan, chestRef) = BuildGroupLoadPlan(group, location, reloadable);
        var loadByTile = plan?.Assignments.ToDictionary(a => a.Machine.Tile);

        // Queue collect→reload per machine in walking order: collect first (so a ReadyToCollect
        // machine is emptied), then its load (guarded on Empty). The worker visits each machine once.
        foreach (var machineRef in groupMachinesHere)
        {
            if (readyToCollect.Contains(machineRef.Tile))
                Session.MachineSteps.Enqueue(new MachineStep(machineRef, MachineActionKind.Collect, group, null));

            if (loadByTile is not null && loadByTile.TryGetValue(machineRef.Tile, out var assignment))
                Session.MachineSteps.Enqueue(new MachineStep(machineRef, MachineActionKind.Load, group, assignment.Requirement));
        }

        if (plan is not null && chestRef is not null && plan.HasWork)
            Session.MachineReloads.Enqueue(new MachineReloadJob(group, chestRef, plan));
    }

    /// <summary>
    /// Builds the load plan for one group's reloadable machines: validates the input chest is usable
    /// and same-location, probes each reloadable machine for a recipe it accepts against the chest
    /// supply, and plans withdrawals + assignments. Returns <c>(null, null)</c> for collect-only
    /// groups, a cross-location chest, or when nothing loadable was found.
    /// </summary>
    private (MachineLoadPlan? Plan, ChestRef? Chest) BuildGroupLoadPlan(
        MachineGroup group,
        GameLocation location,
        List<(MachineRef Ref, SObject Machine, MachineData Data)> reloadable)
    {
        if (group.Mode != MachineGroupMode.CollectAndReload || group.InputChest is not { } chestRef || reloadable.Count == 0)
            return (null, null);

        if (!string.Equals(chestRef.LocationName, location.NameOrUniqueName, StringComparison.Ordinal))
        {
            // v1: physical fetch is within-location only; a chest elsewhere ⇒ collect-only here.
            ModEntry.ModMonitor.Log(
                $"[Dayswork][machines] group '{group.Id}' input chest is in '{chestRef.LocationName}', not '{location.NameOrUniqueName}'; reload skipped this location (collect-only).",
                DevLog.WarnLevel);
            return (null, null);
        }

        var supply = ReadChestSupply(chestRef, out var samples);
        var probeFarmer = CreateWorkerActionFarmer(reloadable[0].Ref.Tile, location);
        var candidates = new List<MachineLoadCandidate>();
        foreach (var (machineRef, machine, data) in reloadable)
        {
            var candidate = _machineReader.BuildLoadCandidate(machineRef, machine, data, group.InputFilter, supply, samples, probeFarmer, location);
            if (candidate is not null)
                candidates.Add(candidate);
        }

        if (candidates.Count == 0)
        {
            DevLog.Log(
                $"[Dayswork][machines] group '{group.Id}': {reloadable.Count} reloadable machine(s) but no matching inputs in chest.",
                DevLog.WarnLevel);
            return (null, null);
        }

        var plan = MachineInputPlanner.Plan(candidates, supply);
        if (!plan.HasWork)
        {
            DevLog.Log(
                $"[Dayswork][machines] group '{group.Id}': {candidates.Count} matchable machine(s) but chest supply insufficient to fill any.",
                DevLog.WarnLevel);
            return (null, null);
        }

        return (plan, chestRef);
    }

    /// <summary>
    /// True when the machine batch for <paramref name="batch"/>'s location has at least one machine
    /// that can be serviced right now. Probes live state <em>without</em> entering the building, so a
    /// shed/barn whose machines are all mid-cycle (e.g. crystalariums still growing) is skipped from
    /// outside instead of being walked into and immediately walked back out — the common waste in the
    /// idle-machine loop. Only meaningful for off-farm batches; on-farm batches enter no building.
    /// </summary>
    private bool MachineBatchHasReadyWork(WorkBatch batch)
    {
        var scope = Session.Ctx.WorkScopes.Machines;
        if (scope is not { IsEnabled: true })
            return false;

        var location = ResolveMachineBatchLocation(batch.LocationName);
        if (location is null)
            return false;

        return scope.Groups.Any(group => GroupHasReadyMachineWork(group, batch.LocationName, location));
    }

    /// <summary>
    /// Read-only "is anything serviceable here" probe for one group at one location: a machine with
    /// output to collect, or (reload groups) an empty reloadable machine whose same-location input
    /// chest actually holds a loadable recipe. Mirrors <see cref="PlanMachineGroup"/> (which builds the
    /// real steps) so the entry guard and the idle-wait probe never disagree with what the batch will
    /// then find to do. Shared by <see cref="MachineBatchHasReadyWork"/> and
    /// <see cref="AnyManagedMachineReady"/>.
    /// </summary>
    private bool GroupHasReadyMachineWork(MachineGroup group, string locationName, GameLocation location)
    {
        var reloadable = new List<(MachineRef Ref, SObject Machine, MachineData Data)>();
        foreach (var machineRef in group.Machines
                     .Where(machine => string.Equals(machine.LocationName, locationName, StringComparison.Ordinal)))
        {
            var live = _machineReader.Resolve(location, machineRef.Tile, machineRef.ExpectedQualifiedId);
            if (live is null)
                continue;

            var state = _machineReader.Classify(live);
            if (state == MachineReadyState.ReadyToCollect)
                return true; // output to collect is always serviceable

            var wantsReload = group.Mode == MachineGroupMode.CollectAndReload
                && live.GetMachineData() is { } data
                && MachineReader.IsReloadable(data);
            if (state == MachineReadyState.Empty && wantsReload && live.GetMachineData() is { } emptyData)
                reloadable.Add((machineRef, live, emptyData));
        }

        if (group.Mode != MachineGroupMode.CollectAndReload
            || group.InputChest is not { } chestRef
            || reloadable.Count == 0)
            return false;

        // v1: reload fetch is within-location only — a chest elsewhere is collect-only.
        if (!string.Equals(chestRef.LocationName, location.NameOrUniqueName, StringComparison.Ordinal))
            return false;

        var supply = ReadChestSupply(chestRef, out var samples);
        var probeFarmer = CreateWorkerActionFarmer(reloadable[0].Ref.Tile, location);
        foreach (var (machineRef, machine, data) in reloadable)
        {
            var candidate = _machineReader.BuildLoadCandidate(
                machineRef, machine, data, group.InputFilter, supply, samples, probeFarmer, location);
            if (candidate is not null && MachineInputPlanner.Plan(new[] { candidate }, supply).HasWork)
                return true;
        }

        return false;
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
            // This group's collect→reload cycle is done — move on to the next group (or, when none
            // remain, AdvanceMachineGroup completes the batch). PlanMachineGroup enqueues at most one
            // reload job per group, so this branch is reached exactly once per group.
            AdvanceMachineGroup();
            return;
        }

        var job = Session.MachineReloads.Dequeue();
        Session.CurrentMachineReload = job;

        var location = Session.CurrentLocation ?? ResolveMachineBatchLocation(Session.MachineBatchLocationName) ?? Game1.getFarm();
        if (!ShiftOrchestrator.TrySelectChestDepositStandTile(job.Chest.Tile, location, Session.Worker, out var standTile))
        {
            // Can't reach the input chest — fall back to collecting this group only. Its collect→reload
            // steps are already queued; the loads no-op against an empty carry buffer, so nothing is
            // withdrawn or lost. (StartNextMachineStep → AdvanceMachineReload advances to the next group.)
            DevLog.Log($"[Dayswork][machines] cannot reach input chest ({job.Chest.Tile.X},{job.Chest.Tile.Y}); collecting this group only.", DevLog.WarnLevel);
            Session.CurrentMachineReload = null;
            StartNextMachineStep();
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

        // The collect→reload steps for this group are already queued (interleaved per machine in
        // PlanMachineGroup); the fetch only had to fill the carry buffer. Drain them now — each
        // machine is collected and then reloaded in a single visit.
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
            if (CarriedCount(id) < need)
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

            // Hand the worker the REAL carried items (flavored roe keeps its preserve id/type + color,
            // so the jar produces Caviar / correctly-flavored Aged Roe), not a flavorless rebuild.
            foreach (var (id, need) in demand)
                foreach (var item in TakeCarried(id, need))
                    who.addItemToInventory(item);

            // Whatever the machine doesn't consume stays with the throwaway farmer — return those real
            // stacks (flavor intact) to the carry buffer so inputs are never lost (hard rule 4). Runs on
            // every exit path (input missing, throw, partial/failed load).
            void ReturnUnconsumed()
            {
                foreach (var (id, _) in demand)
                    foreach (var leftover in who.Items
                                 .Where(i => i is not null && string.Equals(i.QualifiedItemId, id, StringComparison.Ordinal))
                                 .ToList())
                        ReturnCarried(id, leftover);
            }

            var inputItem = who.Items.FirstOrDefault(i =>
                i is not null && string.Equals(i.QualifiedItemId, recipe.InputQualifiedId, StringComparison.Ordinal));
            if (inputItem is null)
            {
                ReturnUnconsumed();
                return;
            }

            try
            {
                // Let vanilla emit the machine-specific load sound (PlayEffects, not IsLocalPlayer-gated)
                // only while the player is in this location — silent for off-farm machine work.
                machine.PlaceInMachine(data, inputItem, probe: false, who, showMessages: false, playSounds: Game1.player.currentLocation == location);
            }
            catch (Exception ex)
            {
                ModEntry.ModMonitor.Log($"[Dayswork][machines] load threw at ({step.Machine.Tile.X},{step.Machine.Tile.Y}): {ex.Message}", DevLog.WarnLevel);
            }

            ReturnUnconsumed();
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

    private IReadOnlyDictionary<string, int> ReadChestSupply(ChestRef chestRef, out IReadOnlyDictionary<string, Item> samples)
    {
        var chest = _chestResolver.ResolveChest(chestRef);
        var supply = new Dictionary<string, int>(StringComparer.Ordinal);
        var sampleMap = new Dictionary<string, Item>(StringComparer.Ordinal);
        samples = sampleMap;
        if (chest is null)
            return supply;

        foreach (var item in chest.Items)
            if (item is not null && !string.IsNullOrWhiteSpace(item.QualifiedItemId))
            {
                supply[item.QualifiedItemId] = supply.GetValueOrDefault(item.QualifiedItemId) + item.Stack;

                // Keep one real (flavor-bearing) item per id as the acceptance-probe template, so the
                // machine matcher sees the genuine input (flavored roe etc.), not a generic rebuild.
                if (!sampleMap.ContainsKey(item.QualifiedItemId) && item.getOne() is { } sample)
                    sampleMap[item.QualifiedItemId] = sample;
            }

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

        // An Auto-Grabber input chest is a held object with no world Location/TileLocation of its own,
        // so drive the audio (and the sprite reset below) from the grabber object at the ref's tile.
        var grabber = _chestResolver.ResolveGrabberOwner(job.Chest);

        // The worker pulls inputs directly from the chest's item list (no ItemGrabMenu), so the
        // vanilla open + per-item pickup sounds never fire. Emit them ourselves, mirroring the
        // deposit trip's chest audio: "openChest" on open, then the inventory move cue "dwop" for
        // each item type taken — staggered, since the withdrawal is synchronous and same-frame cues
        // would overlap into one. Gated on the player being in the chest's location so off-farm
        // fetches stay silent (matches every other worker action).
        var chestLoc = grabber?.Location ?? chest.Location;
        var playerHere = chestLoc is not null && Game1.player.currentLocation == chestLoc;
        var chestTile = grabber is not null
            ? new Vector2(job.Chest.Tile.X, job.Chest.Tile.Y)
            : new Vector2(chest.TileLocation.X, chest.TileLocation.Y);
        if (playerHere)
        {
            chest.frameCounter.Value = 5;  // vanilla open trigger (matches DepositTripRunner)
            chestLoc!.playSound("openChest", chestTile);
        }

        var takeSoundIndex = 0;
        foreach (var (id, want) in job.Plan.Withdrawals)
        {
            var taken = RemoveFromChest(chest, id, want);
            if (taken.Count == 0)
                continue;

            if (!Session.CarriedInputs.TryGetValue(id, out var carried))
            {
                carried = new List<Item>();
                Session.CarriedInputs[id] = carried;
            }
            carried.AddRange(taken);

            if (playerHere)
                DelayedAction.playSoundAfterDelay("dwop", 150 + takeSoundIndex * 90, chestLoc, chestTile);
            takeSoundIndex++;
        }

        // Mirror the vanilla grabItemFromAutoGrabber empties-reset so the grabber sprite returns to
        // "empty" once the worker drains it (no-op for a plain chest).
        if (grabber is not null && chest.isEmpty())
            grabber.showNextIndex.Value = false;
    }

    // Removes up to `amount` units of `qualifiedId` from the chest, returning the real item stacks
    // taken (the taken portion is cloned via getOne so flavored/colored identity — Sturgeon Roe etc. —
    // rides along intact; reconstructing from the bare id would strip it, hard rule 4).
    private static List<Item> RemoveFromChest(Chest chest, string qualifiedId, int amount)
    {
        var taken = new List<Item>();
        var remaining = amount;
        for (var i = chest.Items.Count - 1; i >= 0 && remaining > 0; i--)
        {
            var item = chest.Items[i];
            if (item is null || !string.Equals(item.QualifiedItemId, qualifiedId, StringComparison.Ordinal))
                continue;

            var take = Math.Min(item.Stack, remaining);
            if (item.getOne() is { } piece)
            {
                piece.Stack = take;
                taken.Add(piece);
            }

            item.Stack -= take;
            remaining -= take;
            if (item.Stack <= 0)
                chest.Items.RemoveAt(i);
        }

        return taken;
    }

    // Settles any inputs still in the carry buffer back into their source chest (mutex-checked), or
    // to automatic overflow if the chest is gone/busy/full. Inputs are never lost (hard rule 4).
    private void SettleCarriedInputs()
    {
        if (_session is null || Session.CarriedInputs.Count == 0)
            return;

        var chest = Session.CarriedInputsChest is { } chestRef ? _chestResolver.ResolveChest(chestRef) : null;
        var chestWritable = chest is not null && !chest.GetMutex().IsLocked();

        // An Auto-Grabber input chest is a held object with no world Location/TileLocation, so drive
        // the audio (and re-fill sprite below) from the grabber object at the ref's tile.
        var grabber = Session.CarriedInputsChest is { } grabberRef ? _chestResolver.ResolveGrabberOwner(grabberRef) : null;

        // Returning leftover inputs is the inverse of WithdrawInputs and writes the chest directly,
        // so emit the same hand-rolled chest audio (the menu sounds never fire): "openChest" once,
        // then "dwop" per item type actually returned (staggered, since this is synchronous). Gated
        // on the player being in the chest's location so off-farm settles stay silent.
        var chestLoc = chestWritable ? (grabber?.Location ?? chest!.Location) : null;
        var playerHere = chestLoc is not null && Game1.player.currentLocation == chestLoc;
        var chestTile = grabber is not null && Session.CarriedInputsChest is { } gRef
            ? new Vector2(gRef.Tile.X, gRef.Tile.Y)
            : chest is not null ? new Vector2(chest.TileLocation.X, chest.TileLocation.Y) : Vector2.Zero;
        var chestOpened = false;
        var returnSoundIndex = 0;

        foreach (var (id, stacks) in Session.CarriedInputs.ToList())
        {
            foreach (var carried in stacks)
            {
                if (carried is null || carried.Stack <= 0)
                    continue;

                var count = carried.Stack;
                Item? leftover = carried;   // the real item, flavor/color/quality intact
                if (chestWritable)
                    leftover = chest!.addItem(carried);

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
                {
                    // Preserve flavored identity through overflow too (hard rule 4): capture the real
                    // item so the overflow dispatch clones it back via the flavor templates instead of
                    // rebuilding a generic item from the id.
                    var flavorId = Session.Flavors.Register(leftover);
                    var quality = (leftover as SObject)?.Quality ?? 0;
                    Session.Ctx.Overflow.Add(new OverflowItem(
                        new RoutedItemStack(id, leftover.Stack, TaskKind.HarvestCrops, OutputScopeProvenance.Unknown, quality, flavorId),
                        OverflowReason.NotDelivered));
                }
            }
        }

        // If leftovers were returned into a grabber, restore its "full" sprite (mirrors the vanilla
        // grab; no-op for a plain chest or when the grabber ended up empty).
        if (grabber is not null && chest is not null && !chest.isEmpty())
            grabber.showNextIndex.Value = true;

        Session.CarriedInputs.Clear();
        Session.CarriedInputsChest = null;
    }

    /// <summary>Total units of <paramref name="id"/> currently in the carry buffer (sum of real stacks).</summary>
    private int CarriedCount(string id) =>
        Session.CarriedInputs.TryGetValue(id, out var stacks) ? stacks.Sum(i => i?.Stack ?? 0) : 0;

    /// <summary>
    /// Removes up to <paramref name="count"/> units of <paramref name="id"/> from the carry buffer,
    /// returning the real item stacks taken. A stack that overshoots is split with <c>getOne()</c> so
    /// flavored/colored identity and quality are preserved on both the taken piece and the remainder.
    /// </summary>
    private List<Item> TakeCarried(string id, int count)
    {
        var taken = new List<Item>();
        if (count <= 0 || !Session.CarriedInputs.TryGetValue(id, out var stacks))
            return taken;

        var remaining = count;
        while (stacks.Count > 0 && remaining > 0)
        {
            var stack = stacks[0];
            if (stack is null || stack.Stack <= 0)
            {
                stacks.RemoveAt(0);
                continue;
            }

            if (stack.Stack <= remaining)
            {
                taken.Add(stack);
                remaining -= stack.Stack;
                stacks.RemoveAt(0);
            }
            else if (stack.getOne() is { } piece)
            {
                piece.Stack = remaining;
                stack.Stack -= remaining;
                taken.Add(piece);
                remaining = 0;
            }
            else
            {
                break;
            }
        }

        if (stacks.Count == 0)
            Session.CarriedInputs.Remove(id);

        return taken;
    }

    /// <summary>Returns a real item stack to the carry buffer (e.g. the machine left it unconsumed).</summary>
    private void ReturnCarried(string id, Item item)
    {
        if (item is null || item.Stack <= 0)
            return;

        if (!Session.CarriedInputs.TryGetValue(id, out var stacks))
        {
            stacks = new List<Item>();
            Session.CarriedInputs[id] = stacks;
        }

        stacks.Add(item);
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
