using Dayswork.Core.Domain;
using Dayswork.Guards;
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.TerrainFeatures;

namespace Dayswork.Orchestration;

/// <summary>
/// Owns the narrow Harmony boundary around <see cref="Tree.tickUpdate"/>. A standing tree is
/// registered when a worker starts its fall; the exact tree update that later emits trunk loot is
/// then a synchronous, owner-specific capture boundary.
/// </summary>
internal static class TreeDropAttribution
{
    private const int MaxObservedTicks = 240;
    private const int MaxForcedTicks = 1000;

    private static readonly ReferenceOwnershipIndex<Tree, Attribution> Ownership = new();
    private static PatchState _patchState;

    internal static bool CanStartTreeWork => _patchState != PatchState.Failed;
    internal static bool IsInstalled => _patchState == PatchState.Installed;

    internal static void Install(string modId)
    {
        if (_patchState is PatchState.Installed or PatchState.Failed)
            return;

        try
        {
            var original = AccessTools.DeclaredMethod(
                typeof(Tree),
                nameof(Tree.tickUpdate),
                new[] { typeof(GameTime) });
            if (original is null)
                throw new MissingMethodException(typeof(Tree).FullName, $"{nameof(Tree.tickUpdate)}(GameTime)");

            var prefix = new HarmonyMethod(typeof(TreeDropAttribution), nameof(BeforeTick))
            {
                priority = Priority.Last,
            };
            var postfix = new HarmonyMethod(typeof(TreeDropAttribution), nameof(AfterTick))
            {
                priority = Priority.First,
            };
            var finalizer = new HarmonyMethod(typeof(TreeDropAttribution), nameof(FinalizeTick))
            {
                priority = Priority.First,
            };

            new Harmony(modId).Patch(original, prefix, postfix, transpiler: null, finalizer: finalizer);
            _patchState = PatchState.Installed;
        }
        catch (Exception ex)
        {
            _patchState = PatchState.Failed;
            ModEntry.ModMonitor.Log(
                $"[Dayswork] Could not install the Tree.tickUpdate attribution hook; standing-tree work is disabled for this session. {ex}",
                DevLog.WarnLevel);
        }
    }

    /// <summary>Associates the actual falling tree with one shift. The vanilla host farmer id is
    /// intentionally absent: all worker action farmers share it.</summary>
    internal static bool Register(
        Tree tree,
        GameLocation location,
        ShiftOrchestrator recipient,
        long ownerId,
        Guid officeId,
        OutputScopeProvenance provenance)
    {
        if (!Authority.IsHost || !IsInstalled || !tree.falling.Value)
            return false;

        var attribution = new Attribution(
            tree,
            location,
            tree.Tile,
            recipient,
            ownerId,
            officeId,
            provenance,
            MaxObservedTicks);
        if (Ownership.TryAdd(tree, attribution))
            return true;

        if (Ownership.TryGetValue(tree, out var existing) &&
            ReferenceEquals(existing.Recipient, recipient) &&
            existing.OwnerId == ownerId &&
            existing.OfficeId == officeId)
            return true;

        ModEntry.ModMonitor.Log(
            $"[Dayswork] A falling tree already belongs to another shift; its existing attribution was preserved (loc={location.NameOrUniqueName}, tile={tree.Tile}).",
            DevLog.WarnLevel);
        return false;
    }

    /// <summary>Advances trees that vanilla won't tick because their location is offscreen, or
    /// because the registered instance was removed/replaced at its old tile. Onscreen trees keep
    /// their normal animation until the bounded wait expires.</summary>
    internal static void AdvanceFor(ShiftOrchestrator recipient)
    {
        if (!Authority.IsHost)
            return;

        foreach (var pair in Ownership.Snapshot(a => ReferenceEquals(a.Recipient, recipient)))
        {
            var attribution = pair.Value;
            if (!attribution.Tree.falling.Value)
            {
                Ownership.Remove(attribution.Tree);
                continue;
            }

            attribution.TicksRemaining--;
            var stillAtRegisteredTile =
                attribution.Location.terrainFeatures.TryGetValue(attribution.Tile, out var current) &&
                ReferenceEquals(current, attribution.Tree);
            if (attribution.Location != Game1.currentLocation ||
                !stillAtRegisteredTile ||
                attribution.TicksRemaining <= 0)
                DriveToCompletion(attribution);
        }
    }

    /// <summary>Completes every registered fall for this shift before sleep/suspension settlement
    /// discards its buffer. If another mod prevents completion, the association is retired and any
    /// debris left in the world remains collectible rather than being deleted.</summary>
    internal static void FlushFor(ShiftOrchestrator recipient)
    {
        if (!Authority.IsHost)
            return;

        foreach (var pair in Ownership.Snapshot(a => ReferenceEquals(a.Recipient, recipient)))
            DriveToCompletion(pair.Value);
    }

    internal static int CountFor(ShiftOrchestrator recipient) =>
        Ownership.Snapshot(a => ReferenceEquals(a.Recipient, recipient)).Count;

    internal static void ClearFor(ShiftOrchestrator recipient) =>
        Ownership.RemoveWhere(a => ReferenceEquals(a.Recipient, recipient));

    private static void DriveToCompletion(Attribution attribution)
    {
        try
        {
            var time = Game1.currentGameTime;
            for (var guard = 0; guard < MaxForcedTicks && attribution.Tree.falling.Value; guard++)
                attribution.Tree.tickUpdate(time);
        }
        catch (Exception ex)
        {
            // The patch finalizer has already captured anything emitted before the original threw.
            // Preserve that exception for normal game handling during ordinary ticks; this forced
            // teardown path logs it and leaves any uncaptured debris in the world.
            ModEntry.ModMonitor.Log(
                $"[Dayswork] A registered tree fall could not be completed during settlement (loc={attribution.Location.NameOrUniqueName}, tile={attribution.Tile}). Output already emitted was preserved. {ex}",
                DevLog.WarnLevel);
        }
        finally
        {
            if (attribution.Tree.falling.Value)
            {
                ModEntry.ModMonitor.Log(
                    $"[Dayswork] A registered tree fall did not settle after {MaxForcedTicks} updates; its ownership marker was released and any later vanilla drops will remain in the world (loc={attribution.Location.NameOrUniqueName}, tile={attribution.Tile}).",
                    DevLog.WarnLevel);
            }

            Ownership.Remove(attribution.Tree);
        }
    }

    // Prefix runs last and postfix first. This narrows the snapshot to the verified vanilla
    // original as far as Harmony ordering permits: earlier prefixes are in the baseline and later
    // postfix additions aren't claimed. Compatibility with mods that suppress/replace the original
    // still requires an in-game smoke pass.
    private static void BeforeTick(Tree __instance, out TickCapture? __state)
    {
        __state = null;
        if (!Authority.IsHost || !IsInstalled || !Ownership.TryGetValue(__instance, out var attribution))
            return;

        __state = new TickCapture(
            attribution,
            new HashSet<Debris>(attribution.Location.debris, ReferenceEqualityComparer.Instance));
    }

    private static void AfterTick(Tree __instance, TickCapture? __state) =>
        CompleteTick(__instance, __state);

    private static Exception? FinalizeTick(Tree __instance, TickCapture? __state, Exception? __exception)
    {
        CompleteTick(__instance, __state);
        return __exception;
    }

    private static void CompleteTick(Tree tree, TickCapture? state)
    {
        if (state is null || !state.TryComplete())
            return;

        var attribution = state.Attribution;
        if (!Ownership.TryGetValue(tree, out var current) || !ReferenceEquals(current, attribution))
            return;

        try
        {
            var emitted = SelectNewReferences(attribution.Location.debris, state.Baseline);
            attribution.Recipient.CaptureAttributedTreeDebris(
                attribution.OwnerId,
                attribution.OfficeId,
                attribution.Location,
                emitted,
                attribution.Provenance);
        }
        catch (Exception ex)
        {
            // A hook failure must not replace a vanilla exception or delete output it couldn't own.
            // Successfully buffered debris was removed one item at a time; everything else remains.
            ModEntry.ModMonitor.Log(
                $"[Dayswork] Failed to route worker-felled tree output; uncaptured debris remains in the world (loc={attribution.Location.NameOrUniqueName}, tile={attribution.Tile}). {ex}",
                DevLog.WarnLevel);
        }
        finally
        {
            if (!tree.falling.Value)
                Ownership.Remove(tree);
        }
    }

    internal static IReadOnlyList<T> SelectNewReferences<T>(
        IEnumerable<T> after,
        ISet<T> baseline)
        where T : class =>
        after.Where(item => !baseline.Contains(item)).ToList();

    internal class CompletionGate
    {
        private int _completed;

        public bool TryComplete() => Interlocked.Exchange(ref _completed, 1) == 0;
    }

    private sealed class TickCapture : CompletionGate
    {
        public TickCapture(Attribution attribution, HashSet<Debris> baseline)
        {
            Attribution = attribution;
            Baseline = baseline;
        }

        public Attribution Attribution { get; }
        public HashSet<Debris> Baseline { get; }
    }

    private sealed record Attribution(
        Tree Tree,
        GameLocation Location,
        Vector2 Tile,
        ShiftOrchestrator Recipient,
        long OwnerId,
        Guid OfficeId,
        OutputScopeProvenance Provenance,
        int InitialTicks)
    {
        public int TicksRemaining { get; set; } = InitialTicks;
    }

    private enum PatchState
    {
        Uninitialized,
        Installed,
        Failed,
    }
}
