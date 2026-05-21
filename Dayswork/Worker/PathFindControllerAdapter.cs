using Dayswork.Core.Domain;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Pathfinding;

namespace Dayswork.Worker;

/// <summary>
/// Wraps native NPC PathFindController walking introduced in U-13.
///
/// Replaces the U-10 warpCharacter teleport stub so that the worker actually walks
/// across the farm, which is required for StuckDetector (S-16) to be meaningful and
/// for TODO-01 (tree-seed drops) to be re-checked at a realistic pace.
///
/// The game's own NPC.update() loop advances PathFindController each tick; this adapter
/// only starts/queries the controller. HasArrived / NavigationFailed are polled by
/// ShiftOrchestrator on sampled ticks (PERF-U13-01 / Pattern D).
/// </summary>
internal sealed class PathFindControllerAdapter
{
    public bool HasArrived     { get; private set; } = true;
    public bool NavigationFailed { get; private set; }

    public void StartNavigation(TileCoord destination, GameLocation location, NPC npc)
    {
        NavigationFailed = false;
        HasArrived       = false;

        npc.controller = new PathFindController(
            npc,
            location,
            new Point(destination.X, destination.Y),
            -1,            // any final facing direction
            OnPathFinished);

        // Evaluate immediately whether a path was found.
        if (npc.controller.pathToEndPoint == null)
        {
            // Pathfinding failed — no route exists.
            npc.controller   = null;
            NavigationFailed = true;
            return;
        }

        if (npc.controller.pathToEndPoint.Count == 0)
        {
            // Zero-length path: worker is already on the destination tile.
            npc.controller = null;
            HasArrived     = true;
        }
    }

    public void Clear()
    {
        HasArrived       = true;
        NavigationFailed = false;
        // NOTE: The caller is responsible for clearing npc.controller if the NPC is still live.
        // In practice, Clear() is only called during OnSaving/exit after the NPC is removed.
    }

    // ── Private ──────────────────────────────────────────────────────────────

    private void OnPathFinished(Character character, GameLocation location)
    {
        HasArrived = true;
        if (character is NPC n)
            n.controller = null;
    }
}
