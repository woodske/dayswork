using Dayswork.Core.Domain;
using Dayswork.Core.Shifts;
using StardewValley;

namespace Dayswork.Orchestration;

internal sealed partial class ShiftOrchestrator
{
    // ── Work-activity dispatch ────────────────────────────────────────────────
    //
    // The per-tick arrival/failure dispatch used to be two mirror-image if-chains inside
    // HandleMovement, each hard-wiring every feature's mode (machines → fish ponds → managed crops
    // → animal/tile work). Adding a "Manage X" feature meant editing both chains. That logic now
    // lives in ordered IWorkActivity handlers: HandleMovement walks the list, and each activity
    // either consumes the arrival/failure (returns true) or defers to the next (returns false) —
    // exactly the return-vs-fall-through of the old chain, in the same order. The next feature adds
    // one entry here instead of a branch in two dispatch points.
    //
    // This is a structure-only refactor: each handler contains the former branch verbatim. Per-shift
    // step state still lives on ShiftSession (activities read/write it via the enclosing orchestrator,
    // which nested types may access); moving that state into the activities is a later, separate step.

    private interface IWorkActivity
    {
        /// <summary>Consume the arrival for this activity (true), or let the next activity try (false).</summary>
        bool TryHandleArrival(GameLocation location);

        /// <summary>Consume the navigation failure for this activity (true), or defer to the next (false).</summary>
        bool TryHandleNavigationFailure(GameLocation location);
    }

    private IReadOnlyList<IWorkActivity>? _workActivities;

    // Order is load-bearing — it is the former if-chain order. BatchWorkActivity is terminal (always
    // handles: animal work, non-actionable tile work, or the default "perform the pending task").
    private IReadOnlyList<IWorkActivity> WorkActivities => _workActivities ??= new IWorkActivity[]
    {
        new MachineActivity(this),
        new FishPondActivity(this),
        new ManagedCropActivity(this),
        new BatchWorkActivity(this),
    };

    private sealed class MachineActivity : IWorkActivity
    {
        private readonly ShiftOrchestrator _o;
        public MachineActivity(ShiftOrchestrator o) => _o = o;

        public bool TryHandleArrival(GameLocation location)
        {
            var s = _o.Session;
            if (!s.MachinesActive) return false;

            if (s.MachineFetchPending)
            {
                _o.OnMachineFetchArrived();
                return true;
            }

            if (s.CurrentMachineStep is { } machineStep)
            {
                s.Ctx.StateMachine.SetIntent(new IntentPerformMachineAction(machineStep.Machine, machineStep.Kind));
                s.ActionPending = false;
                return true;
            }

            return false;
        }

        public bool TryHandleNavigationFailure(GameLocation location)
        {
            var s = _o.Session;
            if (!s.MachinesActive) return false;

            if (s.MachineFetchPending)
            {
                // Couldn't reach the input chest tile — collect this group only. Its collect→reload
                // steps are already queued; the loads no-op with an empty carry buffer, so nothing is
                // withdrawn or lost.
                s.MachineFetchPending = false;
                if (s.CurrentMachineReload is { } fetchJob
                    && !string.Equals(fetchJob.Chest.LocationName, s.MachineBatchLocationName, StringComparison.Ordinal))
                {
                    // Walked out to a cross-location chest — return to the machines before collecting.
                    _o.StartMachineReturnExcursion();
                }
                else
                {
                    s.CurrentMachineReload = null;
                    _o.StartNextMachineStep();
                }
                return true;
            }

            if (s.CurrentMachineStep is not null)
            {
                s.CurrentMachineStep = null;
                _o.StartNextMachineStep();
                return true;
            }

            return false;
        }
    }

    private sealed class FishPondActivity : IWorkActivity
    {
        private readonly ShiftOrchestrator _o;
        public FishPondActivity(ShiftOrchestrator o) => _o = o;

        public bool TryHandleArrival(GameLocation location)
        {
            var s = _o.Session;
            if (s.FishPondsActive && s.CurrentFishPondStep is { } pondStep)
            {
                s.Ctx.StateMachine.SetIntent(new IntentPerformFishPondAction(pondStep.Pond));
                s.ActionPending = false;
                return true;
            }

            return false;
        }

        public bool TryHandleNavigationFailure(GameLocation location)
        {
            var s = _o.Session;
            if (s.FishPondsActive && s.CurrentFishPondStep is not null)
            {
                s.CurrentFishPondStep = null;   // couldn't reach this pond; skip it
                _o.StartNextFishPondStep();
                return true;
            }

            return false;
        }
    }

    private sealed class ManagedCropActivity : IWorkActivity
    {
        private readonly ShiftOrchestrator _o;
        public ManagedCropActivity(ShiftOrchestrator o) => _o = o;

        public bool TryHandleArrival(GameLocation location)
        {
            var s = _o.Session;
            if (s.ManagedActive && s.CurrentManagedAction is { } managedAction)
            {
                s.Ctx.StateMachine.SetIntent(new IntentPerformManagedCropAction(managedAction));
                s.ActionPending = false;
                return true;
            }

            return false;
        }

        public bool TryHandleNavigationFailure(GameLocation location)
        {
            var s = _o.Session;
            if (s.ManagedActive && s.CurrentManagedAction is not null)
            {
                s.CurrentManagedAction = null;
                _o.StartNextManagedAction();
                return true;
            }

            return false;
        }
    }

    // Terminal activity: ordinary animal + tile work, and the default "perform the pending task" /
    // "skip" path. Always consumes (returns true), matching the old chain's fall-through tail.
    private sealed class BatchWorkActivity : IWorkActivity
    {
        private readonly ShiftOrchestrator _o;
        public BatchWorkActivity(ShiftOrchestrator o) => _o = o;

        public bool TryHandleArrival(GameLocation location)
        {
            var s = _o.Session;

            if (s.CurrentAnimalWork is not null)
            {
                var animal = _o._animalHandler.FindLiveAnimal(location, s.CurrentAnimalWork.Animal);
                if (animal is null || !_o.IsAnimalWorkActionable(s.CurrentAnimalWork, animal))
                {
                    s.CurrentAnimalWork = null;
                    _o.StartNextAnimalOrTileOrAdvance();
                    return true;
                }

                s.Ctx.StateMachine.SetIntent(s.CurrentAnimalWork.Task == TaskKind.PetAnimals
                    ? new IntentPetAnimal(s.CurrentAnimalWork.Animal)
                    : new IntentCollectFromAnimal(s.CurrentAnimalWork.Animal));
                return true;
            }

            if (s.CurrentTileWork is not null && !_o.IsTileWorkActionable(s.CurrentTileWork, location))
            {
                s.CurrentTileWork = null;
                _o.StartNextAnimalOrTileOrAdvance();
                return true;
            }

            s.Ctx.StateMachine.SetIntent(new IntentPerformTaskAt(s.PendingTaskTile, s.PendingTask));
            s.ActionPending = false;
            return true;
        }

        public bool TryHandleNavigationFailure(GameLocation location)
        {
            var s = _o.Session;

            if (s.CurrentAnimalWork is not null)
            {
                s.DeferredAnimalWork.Add(s.CurrentAnimalWork);
                s.CurrentAnimalWork = null;
                _o.StartNextAnimalOrTileOrAdvance();
                return true;
            }

            if (s.CurrentTileWork is not null)
            {
                s.DeferredTileWork.Add(s.CurrentTileWork);
                s.CurrentTileWork = null;
                _o.StartNextAnimalOrTileOrAdvance();
                return true;
            }

            ModEntry.ModMonitor.Log(
                $"[Dayswork][nav] failed task={s.PendingTask} nav=({s.PendingNavTile.X},{s.PendingNavTile.Y}) task=({s.PendingTaskTile.X},{s.PendingTaskTile.Y}); skipping.",
                DevLog.WarnLevel);
            _o.AdvanceWorkList(location);
            return true;
        }
    }
}
