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
    private void SampleProgress(GameLocation location)
    {
        if (Session.Worker is null) return;

        var currentTile = Session.Worker.TilePoint;

        // Progress = tile moved OR action in progress.
        bool madeProgress = Session.ActionPending || currentTile != Session.LastTilePos;
        Session.LastTilePos = currentTile;

        // Compute elapsed in-game minutes since last sample.
        // Game.timeOfDay advances in 10-unit steps (e.g. 600 → 610 = 10 in-game minutes).
        int elapsedMinutes = (Game1.timeOfDay - Session.LastSampledGameTime) / 10;
        Session.LastSampledGameTime = Game1.timeOfDay;

        Session.Stuck.RecordTick(madeProgress, elapsedMinutes);

        if (Session.Stuck.ShouldFireStuck())
            BeginStuckEscalation(location);
    }

    private void BeginStuckEscalation(GameLocation _)
    {
        if (Session.TravelPurpose != TravelPurpose.None)
        {
            // A stalled travel resolves through its own failure policy (forced warp or
            // report-to-caller), not the stuck teleport ladder.
            _travel.ForceFailure();
            Session.Stuck.Reset();
            return;
        }

        if (Session.CurrentAnimalWork is not null)
        {
//             ModEntry.ModMonitor.Log(
//                 $"[Dayswork][animal] skipping unreachable animal {Session.CurrentAnimalWork.Animal.DisplayName} ({Session.CurrentAnimalWork.Task}).",
//                 LogLevel.Trace);
            Session.CurrentAnimalWork = null;
            StartNextAnimalOrTileOrAdvance();
            return;
        }

        Session.Ctx.StateMachine.Transition(ShiftPhase.Stuck, new IntentPlayEmote(EmoteQuestion));
    }

    private void HandlePlayEmote(IntentPlayEmote intent, GameLocation location)
    {
        Session.Worker!.doEmote(intent.EmoteId);
        QueueStuckTeleport(location);
    }

    private void QueueStuckTeleport(GameLocation location)
    {
        // Step 3 trigger: already attempted one recovery.
        if (Session.Ctx.RecoveryAttempts >= 1)
        {
            Session.Ctx.StateMachine.Transition(ShiftPhase.Recovering, new IntentTeleportHome());
            return;
        }

        // Find the next reachable active-batch work tile.
        TileCoord? recoveryTile = TrySelectNextActiveWork(location, out _, out var selectedRoute)
            ? selectedRoute.InteractionTile
            : null;

        if (recoveryTile is null)
        {
            // No reachable tile — skip straight to step 3.
            Session.Ctx.StateMachine.Transition(ShiftPhase.Recovering, new IntentTeleportHome());
        }
        else
        {
            Session.Ctx.StateMachine.Transition(ShiftPhase.Recovering, new IntentTeleportToTile(recoveryTile.Value));
        }
    }

    private void HandleTeleportToTile(IntentTeleportToTile intent, GameLocation location)
    {
        // Instant reposition to recovery tile, then resume working.
        CancelActiveTravel();
        Session.Worker!.Position = new Vector2(intent.Destination.X, intent.Destination.Y) * 64f;
        Session.Worker.currentLocation = location;
        Session.CurrentLocation = location;
        _nav.Clear();

        // Switch to post-teleport threshold and reset detector.
        Session.Stuck = new StuckDetector(_config.StuckPostTeleportWaitMinutes);
        Session.LastSampledGameTime = Game1.timeOfDay;
        Session.LastTilePos         = Session.Worker!.TilePoint;
        Session.Ctx.RecoveryAttempts++;

        // Recovering → Working: continue from the teleport tile.
        Session.ActionPending = false;
        // The next work item drives the real nav through route-ranked active-batch selection.
        StartNextAnimalOrTileOrAdvance();
    }

    private void HandleTeleportHome(Farm farm)
    {
        // Step 3: reposition home and end shift via normal Depositing path.
        CancelActiveTravel();
        Session.Worker!.Position = new Vector2(Session.FarmExitTile.X, Session.FarmExitTile.Y) * 64f;
        Session.Worker.currentLocation = farm;
        Session.CurrentLocation = farm;
        _nav.Clear();
        QueueWrapUpNow(ShiftStopReason.StuckAbort);
    }

    private void CheckHitReaction()
    {
        if (_session is null || Session.Worker is null) return;

        bool isSwinging = Game1.player.UsingTool && Game1.player.CurrentTool is MeleeWeapon;
        float dist = Math.Abs(Session.Worker.TilePoint.X - Game1.player.TilePoint.X)
                   + Math.Abs(Session.Worker.TilePoint.Y - Game1.player.TilePoint.Y);
        if (HitReactionPolicy.ShouldTriggerEmote(isSwinging, Session.PlayerWasSwinging, dist, HitRangeTiles))
            Session.Worker.doEmote(EmoteExclamation);

        Session.PlayerWasSwinging = isSwinging;
    }

    private void HandleMovement(GameLocation location)
    {
        if (_nav.NavigationFailed)
        {
            if (Session.MachinesActive)
            {
                if (Session.MachineFetchPending)
                {
                    Session.MachineFetchPending = false;
                    AdvanceMachineReload();   // couldn't reach chest; skip this reload job
                    return;
                }

                if (Session.CurrentMachineStep is not null)
                {
                    Session.CurrentMachineStep = null;
                    StartNextMachineStep();
                    return;
                }
            }

            if (Session.FishPondsActive && Session.CurrentFishPondStep is not null)
            {
                Session.CurrentFishPondStep = null;   // couldn't reach this pond; skip it
                StartNextFishPondStep();
                return;
            }

            if (Session.ManagedActive && Session.CurrentManagedAction is not null)
            {
                Session.CurrentManagedAction = null;
                StartNextManagedAction();
                return;
            }

            if (Session.CurrentAnimalWork is not null)
            {
//                 ModEntry.ModMonitor.Log(
//                     $"[Dayswork][animal] navigation failed for {Session.CurrentAnimalWork.Animal.DisplayName} ({Session.CurrentAnimalWork.Task}); deferring within active batch.",
//                     LogLevel.Trace);
                Session.DeferredAnimalWork.Add(Session.CurrentAnimalWork);
                Session.CurrentAnimalWork = null;
                StartNextAnimalOrTileOrAdvance();
                return;
            }

            if (Session.CurrentTileWork is not null)
            {
//                 ModEntry.ModMonitor.Log(
//                     $"[Dayswork][nav] failed task={Session.CurrentTileWork.Task} nav=({Session.PendingNavTile.X},{Session.PendingNavTile.Y}) task=({Session.CurrentTileWork.TaskTile.X},{Session.CurrentTileWork.TaskTile.Y}); deferring within active batch.",
//                     LogLevel.Trace);
                Session.DeferredTileWork.Add(Session.CurrentTileWork);
                Session.CurrentTileWork = null;
                StartNextAnimalOrTileOrAdvance();
                return;
            }

            ModEntry.ModMonitor.Log(
                $"[Dayswork][nav] failed task={Session.PendingTask} nav=({Session.PendingNavTile.X},{Session.PendingNavTile.Y}) task=({Session.PendingTaskTile.X},{Session.PendingTaskTile.Y}); skipping.",
                DevLog.WarnLevel);
            AdvanceWorkList(location);
            return;
        }

        if (_nav.HasArrived)
        {
            if (Session.MachinesActive)
            {
                if (Session.MachineFetchPending)
                {
                    OnMachineFetchArrived();
                    return;
                }

                if (Session.CurrentMachineStep is { } machineStep)
                {
                    Session.Ctx.StateMachine.SetIntent(new IntentPerformMachineAction(machineStep.Machine, machineStep.Kind));
                    Session.ActionPending = false;
                    return;
                }
            }

            if (Session.FishPondsActive && Session.CurrentFishPondStep is { } pondStep)
            {
                Session.Ctx.StateMachine.SetIntent(new IntentPerformFishPondAction(pondStep.Pond));
                Session.ActionPending = false;
                return;
            }

            if (Session.ManagedActive && Session.CurrentManagedAction is { } managedAction)
            {
                Session.Ctx.StateMachine.SetIntent(new IntentPerformManagedCropAction(managedAction));
                Session.ActionPending = false;
                return;
            }

            if (Session.CurrentAnimalWork is not null)
            {
                var animal = _animalHandler.FindLiveAnimal(location, Session.CurrentAnimalWork.Animal);
                if (animal is null || !IsAnimalWorkActionable(Session.CurrentAnimalWork, animal))
                {
                    Session.CurrentAnimalWork = null;
                    StartNextAnimalOrTileOrAdvance();
                    return;
                }

                Session.Ctx.StateMachine.SetIntent(Session.CurrentAnimalWork.Task == TaskKind.PetAnimals
                    ? new IntentPetAnimal(Session.CurrentAnimalWork.Animal)
                    : new IntentCollectFromAnimal(Session.CurrentAnimalWork.Animal));
                return;
            }

            if (Session.CurrentTileWork is not null && !IsTileWorkActionable(Session.CurrentTileWork, location))
            {
                Session.CurrentTileWork = null;
                StartNextAnimalOrTileOrAdvance();
                return;
            }

//             ModEntry.ModMonitor.Log(
//                 $"[Dayswork][nav] arrived task={Session.PendingTask} nav=({Session.PendingNavTile.X},{Session.PendingNavTile.Y}) task=({Session.PendingTaskTile.X},{Session.PendingTaskTile.Y}) worker=({Session.Worker!.TilePoint.X},{Session.Worker.TilePoint.Y}) fallback={_nav.UsedDirectFallback}.",
//                 LogLevel.Trace);
            Session.Ctx.StateMachine.SetIntent(new IntentPerformTaskAt(Session.PendingTaskTile, Session.PendingTask));
            Session.ActionPending = false;
        }
    }

    internal static int FacingToward(Point from, TileCoord to, int fallback)
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
}
