using Dayswork.Core.Capabilities;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.TerrainFeatures;

namespace Dayswork.Worker;

/// <summary>
/// Maps Stardew Valley object/terrain-feature instances to the pure-Core
/// <see cref="AxeTarget"/> / <see cref="PickTarget"/> enums so that the pure
/// <see cref="Dayswork.Core.Capabilities.CapabilityEvaluator"/> can decide
/// whether the worker's tool level is sufficient.
///
/// Returns null for any object class the classifier cannot map — the caller skips
/// that tile rather than throwing.
/// </summary>
internal static class ObjectTargetClassifier
{
    /// <summary>
    /// Classifies the axe-relevant object at <paramref name="tileVec"/> in
    /// <paramref name="loc"/>. Returns null if the object is not an axe target
    /// (including unknown classes and fruit trees — CapabilityMatrix already
    /// hard-returns false for FruitTree, so we classify it rather than skip early).
    /// </summary>
    public static AxeTarget? ClassifyAxe(Vector2 tileVec, GameLocation loc)
    {
        if (FindResourceClumpAt(tileVec, loc) is { } clump)
            return ClassifyAxeClump(clump.parentSheetIndex.Value);

        return loc.terrainFeatures.TryGetValue(tileVec, out var tf)
            ? tf switch
            {
                Tree tree => ClassifyTree(tree),
                FruitTree => AxeTarget.FruitTree,   // CapabilityMatrix returns false → skipped
                _         => null,
            }
            : null;
    }

    public static ResourceClump? FindResourceClumpAt(Vector2 tileVec, GameLocation loc)
    {
        foreach (var clump in loc.resourceClumps)
        {
            var minX = (int)clump.Tile.X;
            var minY = (int)clump.Tile.Y;
            var maxX = minX + clump.width.Value;
            var maxY = minY + clump.height.Value;

            if ((int)tileVec.X >= minX && (int)tileVec.X < maxX &&
                (int)tileVec.Y >= minY && (int)tileVec.Y < maxY)
                return clump;
        }

        return null;
    }

    /// <summary>
    /// Classifies the pick-relevant object at <paramref name="tileVec"/> in
    /// <paramref name="loc"/>. Returns null if the object is not a pick target.
    /// Handles both <see cref="StardewValley.Object"/> stones/ore and
    /// <see cref="ResourceClump"/> boulders/meteorites.
    /// </summary>
    public static PickTarget? ClassifyPick(Vector2 tileVec, GameLocation loc)
    {
        // ResourceClumps: large boulders and meteorites.
        if (FindResourceClumpAt(tileVec, loc) is { } clump)
            return ClassifyPickClump(clump.parentSheetIndex.Value);

        // Standard objects: stones, ore nodes (parentSheetIndex encodes hardness).
        if (loc.objects.TryGetValue(tileVec, out var obj))
            return ClassifyStoneObject(obj);

        return null;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static AxeTarget ClassifyTree(Tree tree)
    {
        // A tree stump is left after felling — distinguish by health/stump flag.
        // Tree.treeStump is true once the trunk has fallen and only the stump remains.
        // Large hardwood stump (mahogany/regular) is classified LargeStump; others SmallStump.
        if (tree.stump.Value)
        {
            // Large stumps (parentSheetIndex 6 = mahogany; 1–5 = normal species).
            // In practice, all stumps left on the farm require Steel+; classify uniformly.
            return tree.treeType.Value == Tree.mahoganyTree
                ? AxeTarget.LargeStump
                : AxeTarget.SmallStump;
        }

        // Full standing tree — large logs (fallen trunks) are ResourceClumps, not Tree
        // terrain features, so a standing Tree is always StandingTree here.
        return AxeTarget.StandingTree;
    }

    private static AxeTarget? ClassifyAxeClump(int parentSheetIndex) => parentSheetIndex switch
    {
        ResourceClump.stumpIndex     => AxeTarget.LargeStump,
        ResourceClump.hollowLogIndex => AxeTarget.LargeLog,
        _                            => null,
    };

    /// <summary>
    /// Maps ResourceClump parentSheetIndex to PickTarget.
    /// Sheet indices from SDV source: 672/148/752/754/756/758 = boulders/large rocks, 622 = meteorite.
    /// Returns null for unrecognised clumps (e.g. forest logs mapped to AxeTarget elsewhere).
    /// </summary>
    private static PickTarget? ClassifyPickClump(int parentSheetIndex) => parentSheetIndex switch
    {
        ResourceClump.boulderIndex
            or ResourceClump.quarryBoulderIndex
            or ResourceClump.mineRock1Index
            or ResourceClump.mineRock2Index
            or ResourceClump.mineRock3Index
            or ResourceClump.mineRock4Index => PickTarget.LargeBoulder,
        ResourceClump.meteoriteIndex => PickTarget.Meteorite,
        _                            => null,
    };

    /// <summary>
    /// Maps a placed Object to PickTarget by parentSheetIndex.
    /// Standard stones (390) → SmallRock. Ore nodes are treated as SmallRock (any level).
    /// </summary>
    private static PickTarget? ClassifyStoneObject(StardewValley.Object obj)
    {
        // ItemId "390" is the universal "Stone" object.
        // Ore nodes have various parentSheetIndexes but share the same performToolAction behavior.
        // All are breakable with any pickaxe — classify as SmallRock.
        if (obj.Name == "Stone" || IsOreNode(obj))
            return PickTarget.SmallRock;

        return null;
    }

    private static bool IsOreNode(StardewValley.Object obj)
    {
        // IsBreakableStone() covers stones, ore nodes, and other rock-type objects.
        // All farm ore nodes are breakable at any pickaxe level — SmallRock.
        return obj.IsBreakableStone() && obj.Name != "Stone";
    }
}
