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

        // Rebuild the passability grid before choosing a recovery tile — the worker got stuck, which
        // often means the cached grid disagrees with reality (a placed obstacle, a partially-cleared
        // clump). A fresh grid ensures recovery never teleports based on stale reachability.
        Session.Passability.InvalidateLocation(location);

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
        // Ordered activity dispatch (see ShiftOrchestrator.Activities.cs). Each activity mirrors the
        // former if-chain branch: it consumes the event (returns true) or falls through to the next.
        // BatchWorkActivity is terminal, so exactly one activity always handles the event.
        if (_nav.NavigationFailed)
        {
            foreach (var activity in WorkActivities)
                if (activity.TryHandleNavigationFailure(location))
                    return;
            return;
        }

        if (_nav.HasArrived)
        {
            foreach (var activity in WorkActivities)
                if (activity.TryHandleArrival(location))
                    return;
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
