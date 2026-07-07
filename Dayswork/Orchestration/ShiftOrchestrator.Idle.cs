using Dayswork.Core.Domain;
using Dayswork.Core.Machines;
using Dayswork.Core.Shifts;
using Dayswork.Integration;
using Dayswork.Worker;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.GameData.Machines;
using SObject = StardewValley.Object;

namespace Dayswork.Orchestration;

/// <summary>
/// The "idle task" loop. When a contract's <see cref="ContractPreferences.IdleTask"/> names a
/// repeatable task, the worker — instead of going home once first-round work is done — parks
/// outside the office door, watches the managed machines, and services them as they become ready,
/// looping until energy exhaustion or the day's hard time cap (both already route to the normal
/// wrap-up). v1 supports only <see cref="IdleTaskKind.ManageMachines"/>.
///
/// Servicing reuses the machine-batch flow wholesale: a fresh machine-only batch plan is swapped in
/// and run through <see cref="BeginCurrentBatch"/>, which gives collect-then-reload, building/shed
/// travel, energy spend, and wrap-up-on-exhaustion for free.
/// </summary>
internal sealed partial class ShiftOrchestrator
{
    /// <summary>
    /// Called when <see cref="BeginCurrentBatch"/> runs off the end of the plan. Enters (or re-enters)
    /// the idle loop when the contract opted into a repeatable idle task and the shift isn't already
    /// winding down. Returns false to let the caller wrap up and go home as before.
    /// </summary>
    private bool TryEnterIdleLoop()
    {
        if (_session is null || Session.Worker is null)
            return false;

        if (Session.Ctx.Preferences.IdleTask != IdleTaskKind.ManageMachines)
            return false;

        // A pending stop (exhaustion / 8pm cap) always wins over re-entering the loop.
        if (ShouldWrapUpBeforeNextUnit())
            return false;

        if (Session.Ctx.WorkScopes.Machines is not { IsEnabled: true })
            return false;

        // Deposit any items collected this round before parking at the door.
        if (!Session.Ctx.Buffer.IsEmpty)
        {
            Session.DepositResume = DepositResumeMode.ResumeIdle;
            BeginDeposit();
            return true;
        }

        // Skip the walk to the door if there's already work to start.
        if (AnyManagedMachineReady())
        {
            BeginIdleMachineServicing();
            return true;
        }

        BeginIdleWait();
        return true;
    }

    /// <summary>
    /// Called by DepositTripRunner when the pre-idle deposit round finishes.
    /// Honours any stop reason that fired mid-deposit; otherwise parks at the door.
    /// </summary>
    internal void OnPreIdleDepositComplete()
    {
        Session.DepositResume = DepositResumeMode.None;
        if (ShouldWrapUpBeforeNextUnit())
        {
            QueueWrapUpNow(Session.Ctx.PendingStopReason ?? ShiftStopReason.Completed);
            return;
        }
        // Skip the walk to the door if there's already work to start.
        if (AnyManagedMachineReady())
        {
            BeginIdleMachineServicing();
            return;
        }
        BeginIdleWait();
    }

    /// <summary>Announce the wait and walk back to the office door to park.</summary>
    private void BeginIdleWait()
    {
        if (_session is null || Session.Worker is null)
            return;

        Session.IdleWaiting = false;
        CropHudNotifier.IdleWaitingForMachines(CountBusyManagedMachines());

        var farm = Game1.getFarm();
        Session.CurrentLocation = farm;
        StartTravel(
            WalkOnlyPlan(farm, Session.FarmExitTile, TravelFailurePolicy.ReportFailure),
            TravelPurpose.IdleReturn);
    }

    /// <summary>Arrived at the office door: face away from it, emote, and begin polling.</summary>
    private void OnIdleReturnArrived()
    {
        if (_session is null || Session.Worker is null)
            return;

        Session.IdleWaitFacing = ResolveIdleWaitFacing();
        Session.Worker.faceDirection(Session.IdleWaitFacing);
        Session.Worker.doEmote(Emotes.MusicNote);
        Session.IdleWaiting = true;
        Session.IdleWaitTicks = 0;
    }

    /// <summary>The worker faces away from the office human door (so the music note reads as "idle").</summary>
    private int ResolveIdleWaitFacing()
    {
        var building = HiringBuildingInteraction.FindHiringBuilding(Game1.getFarm());
        if (building is null)
            return 2; // default: face down (away from a door to the north)

        var door = building.getPointForHumanDoor();
        // Direction pointing from the door toward the stand tile = away from the building.
        return FacingToward(new Point(door.X, door.Y), Session.FarmExitTile, fallback: 2);
    }

    /// <summary>One throttled tick of the parked-at-the-door wait loop.</summary>
    private void ContinueIdleWaitTick()
    {
        if (_session is null || Session.Worker is null)
            return;

        // Exhaustion or the 8pm cap ends the loop: deposit and head home as normal.
        if (ShouldWrapUpBeforeNextUnit())
        {
            Session.IdleWaiting = false;
            QueueWrapUpNow(Session.Ctx.PendingStopReason ?? ShiftStopReason.Completed);
            return;
        }

        if (AnyManagedMachineReady())
        {
            Session.IdleWaiting = false;
            BeginIdleMachineServicing();
            return;
        }

        // Nothing serviceable yet — keep waiting, re-emoting the music note periodically.
        Session.IdleWaitTicks++;
        if (Session.IdleWaitTicks % 30 == 1)
        {
            Session.Worker.faceDirection(Session.IdleWaitFacing);
            Session.Worker.doEmote(Emotes.MusicNote);
        }
    }

    /// <summary>Swap in a fresh machine-only plan and run it; the loop re-arms when it completes.</summary>
    private void BeginIdleMachineServicing()
    {
        if (_session is null)
            return;

        // Resuming active work after parking at the office: the idle wait skips SampleProgress, so the
        // stuck detector's elapsed-time baseline (LastSampledGameTime) is frozen at park-time. Without
        // this refresh the first servicing tick reads the whole wait as one giant no-progress sample,
        // fires a false "stuck," and (on the second occurrence) teleports the worker home — ending the
        // shift hours before the 8pm cap. Reset both so only real servicing time is measured.
        Session.Stuck.Reset();
        Session.LastSampledGameTime = Game1.timeOfDay;

        Session.Ctx.ResetBatches(new ShiftPlanBuilder().BuildMachineBatchPlan(Session.Ctx.WorkScopes, Session.BatchOrdering));
        BeginCurrentBatch();
    }

    /// <summary>
    /// True when at least one managed machine can be serviced right now: a machine with output to
    /// collect, or (for reload groups) an empty reloadable machine whose input chest — in any
    /// location — actually holds a loadable recipe. Delegates to the shared per-location probe
    /// (<see cref="GroupHasReadyMachineWork"/>, which mirrors <see cref="PlanMachineGroup"/>) so the
    /// wait never wakes up for work that the servicing pass would then find nothing to do.
    /// </summary>
    private bool AnyManagedMachineReady()
    {
        if (_session is null || Session.Worker is null)
            return false;

        var scope = Session.Ctx.WorkScopes.Machines;
        if (scope is not { IsEnabled: true })
            return false;

        foreach (var group in scope.Groups)
        {
            foreach (var locationName in group.Machines
                         .Select(machine => machine.LocationName)
                         .Distinct(StringComparer.Ordinal))
            {
                var location = ResolveMachineBatchLocation(locationName);
                if (location is null)
                    continue;

                if (GroupHasReadyMachineWork(group, locationName, location))
                    return true;
            }
        }

        return false;
    }

    /// <summary>How many managed machines are currently processing toward an output (the HUD "X").</summary>
    private int CountBusyManagedMachines()
    {
        if (_session is null)
            return 0;

        var scope = Session.Ctx.WorkScopes.Machines;
        if (scope is not { IsEnabled: true })
            return 0;

        var counted = new HashSet<(string, int, int)>();
        var busy = 0;
        foreach (var machineRef in scope.AllMachines)
        {
            if (!counted.Add((machineRef.LocationName, machineRef.Tile.X, machineRef.Tile.Y)))
                continue;

            var location = ResolveMachineBatchLocation(machineRef.LocationName);
            if (location is null)
                continue;

            var live = _machineReader.Resolve(location, machineRef.Tile, machineRef.ExpectedQualifiedId);
            if (live is not null && _machineReader.Classify(live) == MachineReadyState.Busy)
                busy++;
        }

        return busy;
    }
}
