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
        if (_farmhand is null) return;

        var currentTile = _farmhand.TilePoint;

        // Progress = tile moved OR action in progress.
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

    private void BeginStuckEscalation(GameLocation _)
    {
        if (_travelPurpose != TravelPurpose.None)
        {
            // A stalled travel resolves through its own failure policy (forced warp or
            // report-to-caller), not the stuck teleport ladder.
            _travel.ForceFailure();
            _stuck.Reset();
            return;
        }

        if (_currentAnimalWork is not null)
        {
//             ModEntry.ModMonitor.Log(
//                 $"[Dayswork][animal] skipping unreachable animal {_currentAnimalWork.Animal.DisplayName} ({_currentAnimalWork.Task}).",
//                 LogLevel.Trace);
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

    private void QueueStuckTeleport(GameLocation location)
    {
        // Step 3 trigger: already attempted one recovery.
        if (_ctx!.RecoveryAttempts >= 1)
        {
            _ctx.StateMachine.Transition(ShiftPhase.Recovering, new IntentTeleportHome());
            return;
        }

        // Find the next reachable active-batch work tile.
        TileCoord? recoveryTile = TrySelectNextActiveWork(location, out _, out var selectedRoute)
            ? selectedRoute.InteractionTile
            : null;

        if (recoveryTile is null)
        {
            // No reachable tile — skip straight to step 3.
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
        CancelActiveTravel();
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
        // The next work item drives the real nav through route-ranked active-batch selection.
        StartNextAnimalOrTileOrAdvance();
    }

    private void HandleTeleportHome(Farm farm)
    {
        // Step 3: reposition home and end shift via normal Depositing path.
        CancelActiveTravel();
        _farmhand!.Position = new Vector2(_farmExitTile.X, _farmExitTile.Y) * 64f;
        _farmhand.currentLocation = farm;
        _currentLocation = farm;
        _nav.Clear();
        QueueWrapUpNow(ShiftStopReason.StuckAbort);
    }

    private void CheckHitReaction()
    {
        if (_farmhand is null || _ctx is null) return;

        bool isSwinging = Game1.player.UsingTool && Game1.player.CurrentTool is MeleeWeapon;
        float dist = Math.Abs(_farmhand.TilePoint.X - Game1.player.TilePoint.X)
                   + Math.Abs(_farmhand.TilePoint.Y - Game1.player.TilePoint.Y);
        if (HitReactionPolicy.ShouldTriggerEmote(isSwinging, _playerWasSwinging, dist, HitRangeTiles))
            _farmhand.doEmote(EmoteExclamation);

        _playerWasSwinging = isSwinging;
    }

    private void HandleMovement(GameLocation location)
    {
        if (_nav.NavigationFailed)
        {
            if (_managedActive && _currentManagedAction is not null)
            {
                _currentManagedAction = null;
                StartNextManagedAction();
                return;
            }

            if (_currentAnimalWork is not null)
            {
//                 ModEntry.ModMonitor.Log(
//                     $"[Dayswork][animal] navigation failed for {_currentAnimalWork.Animal.DisplayName} ({_currentAnimalWork.Task}); deferring within active batch.",
//                     LogLevel.Trace);
                _deferredAnimalWork.Add(_currentAnimalWork);
                _currentAnimalWork = null;
                StartNextAnimalOrTileOrAdvance();
                return;
            }

            if (_currentTileWork is not null)
            {
//                 ModEntry.ModMonitor.Log(
//                     $"[Dayswork][nav] failed task={_currentTileWork.Task} nav=({_pendingNavTile.X},{_pendingNavTile.Y}) task=({_currentTileWork.TaskTile.X},{_currentTileWork.TaskTile.Y}); deferring within active batch.",
//                     LogLevel.Trace);
                _deferredTileWork.Add(_currentTileWork);
                _currentTileWork = null;
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
            if (_managedActive && _currentManagedAction is { } managedAction)
            {
                _ctx!.StateMachine.SetIntent(new IntentPerformManagedCropAction(managedAction));
                _actionPending = false;
                return;
            }

            if (_currentAnimalWork is not null)
            {
                var animal = _animalHandler.FindLiveAnimal(location, _currentAnimalWork.Animal);
                if (animal is null || !IsAnimalWorkActionable(_currentAnimalWork, animal))
                {
                    _currentAnimalWork = null;
                    StartNextAnimalOrTileOrAdvance();
                    return;
                }

                _ctx!.StateMachine.SetIntent(_currentAnimalWork.Task == TaskKind.PetAnimals
                    ? new IntentPetAnimal(_currentAnimalWork.Animal)
                    : new IntentCollectFromAnimal(_currentAnimalWork.Animal));
                return;
            }

            if (_currentTileWork is not null && !IsTileWorkActionable(_currentTileWork, location))
            {
                _currentTileWork = null;
                StartNextAnimalOrTileOrAdvance();
                return;
            }

//             ModEntry.ModMonitor.Log(
//                 $"[Dayswork][nav] arrived task={_pendingTask} nav=({_pendingNavTile.X},{_pendingNavTile.Y}) task=({_pendingTaskTile.X},{_pendingTaskTile.Y}) worker=({_farmhand!.TilePoint.X},{_farmhand.TilePoint.Y}) fallback={_nav.UsedDirectFallback}.",
//                 LogLevel.Trace);
            _ctx!.StateMachine.SetIntent(new IntentPerformTaskAt(_pendingTaskTile, _pendingTask));
            _actionPending = false;
        }
    }

    private int FacingTowardDestination()
    {
        if (_currentTrip is null || _farmhand is null)
            return _farmhand?.FacingDirection ?? 2;
        return FacingToward(_farmhand.TilePoint, _currentTrip.Tile, _farmhand.FacingDirection);
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
}
