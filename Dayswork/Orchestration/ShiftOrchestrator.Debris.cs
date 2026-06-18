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
    private bool CollectNewDebrisAtTile(
        HashSet<Debris> before,
        GameLocation loc,
        TaskKind sourceTask,
        Vector2 tileVec,
        OutputScopeProvenance provenance) =>
        CollectNewDebris(
            before,
            loc,
            sourceTask,
            new Vector2(tileVec.X * 64f + 32f, tileVec.Y * 64f + 32f),
            ImmediateDebrisSweepRadiusTiles,
            provenance);

    private bool CollectNewDebris(
        HashSet<Debris> before,
        GameLocation loc,
        TaskKind sourceTask,
        Vector2? origin = null,
        int radiusTiles = int.MaxValue,
        OutputScopeProvenance? provenance = null)
    {
        bool collected = false;
        foreach (var d in loc.debris.ToList())
        {
            if (before.Contains(d) ||
                (origin.HasValue && !IsDebrisNear(d, origin.Value, radiusTiles)))
                continue;

            if (!TryGetDebrisItem(d, out var itemId, out var stack))
            {
                LogInvalidDebris(loc, sourceTask, origin, d);
                continue;
            }

            Session.Ctx.Buffer.Add(itemId, stack, sourceTask, provenance ?? OutputScopeProvenance.Unknown);
//             ModEntry.ModMonitor.Log(
//                 $"[Dayswork][debris] collected {stack}x {itemId} from game debris task={sourceTask} chunks={d.Chunks.Count} debrisType={d.debrisType.Value} chunkType={d.chunkType.Value}.",
//                 LogLevel.Trace);
            loc.debris.Remove(d);
            collected = true;
        }
        return collected;
    }

    private static bool TryGetDebrisItem(Debris debris, out string itemId, out int stack)
    {
        if (debris.item is not null)
        {
            stack = Math.Max(1, debris.item.Stack);
            return DebrisItemIdResolver.TryResolveCollectibleItemId(debris.item.QualifiedItemId, out itemId);
        }

        var debrisItemId = debris.itemId.Value;
        if (DebrisItemIdResolver.TryResolveCollectibleItemId(debrisItemId, out itemId))
        {
            stack = debris.debrisType.Value == Debris.DebrisType.RESOURCE
                ? Math.Max(1, debris.Chunks.Count)
                : 1;
            return true;
        }

        itemId = "";
        stack = 0;
        return false;
    }

    private static void LogInvalidDebris(GameLocation loc, TaskKind sourceTask, Vector2? origin, Debris debris)
    {
        if (debris.item is null && string.IsNullOrWhiteSpace(debris.itemId.Value))
            return;

        var rawItemId = debris.item?.QualifiedItemId ?? debris.itemId.Value ?? "";
        var rawDisplayName = debris.item?.DisplayName ?? "<none>";
        var originText = origin.HasValue
            ? $"({(int)(origin.Value.X / 64f)},{(int)(origin.Value.Y / 64f)})"
            : "<none>";

        ModEntry.ModMonitor.Log(
            $"[Dayswork][debris] worker-created debris could not be resolved to a valid item id raw='{rawItemId}' display='{rawDisplayName}' loc={loc.Name} task={sourceTask} origin={originText} chunks={debris.Chunks.Count} debrisType={debris.debrisType.Value} chunkType={debris.chunkType.Value}.",
            DevLog.WarnLevel);
    }

    private static bool TryGetRemovedStandardStoneDrop(StardewValley.Object obj, out string itemId, out int stack)
    {
        if (obj.QualifiedItemId == "(O)390" || obj.ItemId == "390" || obj.Name == "Stone")
        {
            itemId = "(O)390";
            stack = 1;
            return true;
        }

        itemId = "";
        stack = 0;
        return false;
    }

    private void QueueDelayedDebrisSweep(
        GameLocation loc,
        Vector2 tileVec,
        HashSet<Debris> baseline,
        TaskKind sourceTask,
        OutputScopeProvenance provenance)
    {
        var origin = new Vector2(tileVec.X * 64f + 32f, tileVec.Y * 64f + 32f);
        Session.PendingDebrisSweeps.Add(new PendingDebrisSweep(
            loc,
            origin,
            baseline,
            DelayedTreeDebrisSweepTicks,
            DelayedTreeDebrisSweepRadiusTiles,
            sourceTask,
            provenance));
    }

    private void ProcessPendingDebrisSweeps()
    {
        for (var i = Session.PendingDebrisSweeps.Count - 1; i >= 0; i--)
        {
            var sweep = Session.PendingDebrisSweeps[i];
            AdvanceOffscreenTreeFall(sweep);
            CollectNewDebris(sweep.Baseline, sweep.Location, sweep.SourceTask, sweep.Origin, sweep.RadiusTiles, sweep.Provenance);
            sweep.TicksRemaining--;
            if (sweep.TicksRemaining <= 0)
                Session.PendingDebrisSweeps.RemoveAt(i);
        }
    }

    private void FlushPendingDebrisSweeps()
    {
        foreach (var sweep in Session.PendingDebrisSweeps)
        {
            AdvanceOffscreenTreeFall(sweep);
            CollectNewDebris(sweep.Baseline, sweep.Location, sweep.SourceTask, sweep.Origin, sweep.RadiusTiles, sweep.Provenance);
        }

        Session.PendingDebrisSweeps.Clear();
    }

    // A non-current GameLocation is ticked by GameLocation.updateEvenIfFarmerIsntHere, which does NOT
    // tick terrainFeatures — so a worker-felled tree's fall animation freezes and never spawns its
    // trunk debris until the player walks back onto the location (at which point the fall finishes,
    // playing treethud and dropping wood with no worker present). Drive the fall to completion here so
    // the debris lands in loc.debris now — silently, since localSound no-ops off-screen — and the
    // sweep can collect it. No-op for the fruit-tree and stump-removal sweeps that share this path:
    // their debris spawns synchronously and the tree isn't in a falling state.
    private static void AdvanceOffscreenTreeFall(PendingDebrisSweep sweep)
    {
        if (sweep.Location == Game1.currentLocation) return;

        var tile = new Vector2(
            (float)Math.Round((sweep.Origin.X - 32f) / 64f),
            (float)Math.Round((sweep.Origin.Y - 32f) / 64f));

        if (!sweep.Location.terrainFeatures.TryGetValue(tile, out var tf)
            || tf is not Tree tree || !tree.falling.Value)
            return;

        var time = Game1.currentGameTime;
        for (var guard = 0; guard < 1000 && tree.falling.Value; guard++)
            tree.tickUpdate(time);
    }

    private static bool IsDebrisNear(Debris debris, Vector2 origin, int radiusTiles)
    {
        var radiusPixels = radiusTiles * 64f;
        var radiusSq = radiusPixels * radiusPixels;

        foreach (var chunk in debris.Chunks)
        {
            if (Vector2.DistanceSquared(chunk.position.Value, origin) <= radiusSq)
                return true;
        }

        return false;
    }
}
