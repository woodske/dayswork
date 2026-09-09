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

    // Sweep worker drops that a vanilla API spawned into a location other than the one the worker
    // is actually working — see InvokeTaskActionGuarded for why ResourceClump.destroy() leaks loot
    // into Game1.currentLocation. The leaked drops land at the work tile's coordinates but in the
    // foreign location, so no origin filter is applied: every debris not present before the beat is
    // worker-created and is redirected into the buffer (and removed from the foreign location).
    private void CollectLeakedWorkerDebris(
        HashSet<Debris> before,
        GameLocation leakLocation,
        GameLocation workLocation,
        TaskKind sourceTask,
        TileCoord tile)
    {
        if (!CollectNewDebris(before, leakLocation, sourceTask,
                origin: null, radiusTiles: int.MaxValue, Session.PendingOutputProvenance))
            return;

        ModEntry.ModMonitor.Log(
            $"[Dayswork][debris] redirected worker drops that vanilla spawned into {leakLocation.NameOrUniqueName} " +
            $"(player away from work location {workLocation.NameOrUniqueName}) during task={sourceTask} at ({tile.X},{tile.Y}).",
            DevLog.WarnLevel);
    }

    // Dev-only leak tripwire (gated by DevLog.Enabled at the call site). INVARIANT: after a worker
    // beat, no worker-created item-debris should remain in a location the worker isn't working —
    // CollectLeakedWorkerDebris must have recovered all of it. `leakedItems` is the count vanilla
    // mis-routed this beat (sampled before recovery); anything still present afterward is STRANDED
    // (an unrecovered leak — e.g. a drop whose id our resolver can't normalize) and is logged loudly
    // plus tallied for the shift-end summary. Scoped to Game1.currentLocation, the only sink vanilla
    // routes loot to (see docs/game-data/debris-and-drops.md); it does not scan unrelated locations, so the
    // player's own roaming activity never produces false positives.
    private void AuditForeignLeak(
        HashSet<Debris> before,
        GameLocation leakLocation,
        GameLocation workLocation,
        TaskKind sourceTask,
        TileCoord tile,
        int leakedItems)
    {
        if (leakedItems <= 0)
            return; // vanilla routed nothing into the player's location this beat

        var stranded  = CountNewItemDebris(before, leakLocation);
        var recovered = leakedItems - stranded;

        Session.LeakBeatsObserved++;
        Session.LeakItemsRecovered += recovered;
        Session.LeakItemsStranded  += stranded;

        DevLog.Log(
            $"[Dayswork][leak-detector] task={sourceTask} at ({tile.X},{tile.Y}) on {workLocation.NameOrUniqueName}: " +
            $"vanilla mis-routed {leakedItems} item(s) into {leakLocation.NameOrUniqueName} (player away); " +
            $"recovered {recovered}, STRANDED {stranded}.",
            stranded > 0 ? LogLevel.Warn : LogLevel.Trace);
    }

    // Sums the stacks of item-bearing debris in `loc` that weren't present in `before`. Mirrors the
    // stack accounting in TryGetDebrisItem so the leak count matches what recovery would buffer.
    private static int CountNewItemDebris(HashSet<Debris> before, GameLocation loc)
    {
        var count = 0;
        foreach (var d in loc.debris)
        {
            if (before.Contains(d))
                continue;

            if (d.item is not null)
                count += Math.Max(1, d.item.Stack);
            else if (!string.IsNullOrWhiteSpace(d.itemId.Value))
                count += d.debrisType.Value == Debris.DebrisType.RESOURCE
                    ? Math.Max(1, d.Chunks.Count)
                    : 1;
        }
        return count;
    }

    // Dev console (dayswork_debug_leaks) + shift-end summary readout of the leak audit tally.
    internal void LogLeakAudit(LogLevel level)
    {
        if (_session is null)
        {
            ModEntry.ModMonitor.Log("[Dayswork][leak-detector] No active shift.", level);
            return;
        }

        ModEntry.ModMonitor.Log(
            $"[Dayswork][leak-detector] this shift: {Session.LeakBeatsObserved} beat(s) mis-routed loot to the " +
            $"player's location; recovered {Session.LeakItemsRecovered}, stranded {Session.LeakItemsStranded}.",
            level);
    }

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

            if (!TryBufferDebris(d, sourceTask, provenance ?? OutputScopeProvenance.Unknown))
            {
                LogInvalidDebris(loc, sourceTask, origin, d);
                continue;
            }

            loc.debris.Remove(d);
            collected = true;
        }
        return collected;
    }

    /// <summary>Transfers one collectible debris object into this shift's buffer without reducing
    /// real items to an id/count pair. Removal from the world is deliberately the caller's next
    /// operation, after this method has established buffer ownership.</summary>
    private bool TryBufferDebris(
        Debris debris,
        TaskKind sourceTask,
        OutputScopeProvenance provenance)
    {
        if (debris.item is not null)
        {
            if (!DebrisItemIdResolver.TryResolveCollectibleItemId(debris.item.QualifiedItemId, out var itemId))
                return false;

            var stack = Math.Max(1, debris.item.Stack);
            var quality = (debris.item as StardewValley.Object)?.Quality ?? 0;
            var flavorId = Session.Flavors.Register(debris.item);
            Session.Ctx.Buffer.Add(itemId, stack, sourceTask, provenance, quality, flavorId);
            return true;
        }

        var debrisItemId = debris.itemId.Value;
        if (!DebrisItemIdResolver.TryResolveCollectibleItemId(debrisItemId, out var resourceItemId))
            return false;

        var resourceStack = debris.debrisType.Value == Debris.DebrisType.RESOURCE
            ? Math.Max(1, debris.Chunks.Count)
            : 1;
        Session.Ctx.Buffer.Add(resourceItemId, resourceStack, sourceTask, provenance);
        return true;
    }

    /// <summary>Receives only the debris emitted by one registered Tree.tickUpdate call. Owner and
    /// office checks make a stale association fail safe: the debris remains in the world.</summary>
    internal void CaptureAttributedTreeDebris(
        long ownerId,
        Guid officeId,
        GameLocation location,
        IReadOnlyList<Debris> emitted,
        OutputScopeProvenance provenance)
    {
        if (_session is null || Session.OwnerId != ownerId || Session.OfficeId != officeId)
        {
            ModEntry.ModMonitor.Log(
                $"[Dayswork] Ignored stale tree-drop attribution for owner={ownerId}, office={officeId:N}; emitted debris remains in {location.NameOrUniqueName}.",
                DevLog.WarnLevel);
            return;
        }

        foreach (var debris in emitted)
        {
            if (!location.debris.Contains(debris))
                continue;

            if (!TryBufferDebris(debris, TaskKind.CutTrees, provenance))
            {
                LogInvalidDebris(location, TaskKind.CutTrees, origin: null, debris);
                continue;
            }

            location.debris.Remove(debris);
        }
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
        if (obj.QualifiedItemId == GameItemIds.Stone || obj.Name == "Stone")
        {
            itemId = GameItemIds.Stone;
            stack = 1;
            return true;
        }

        itemId = "";
        stack = 0;
        return false;
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
