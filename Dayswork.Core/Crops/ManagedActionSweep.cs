namespace Dayswork.Core.Crops;

using Dayswork.Core.Domain;
using Dayswork.Core.Geometry;

/// <summary>
/// Orders a location's merged managed-crop actions (all zones together) into one serpentine field
/// sweep, replacing the old zone-by-zone, two-passes-per-zone execution order. Per-tile action
/// chains stay intact via <see cref="ManagedCropShiftPlan.ActionRank"/> (harvest → clear → till →
/// fertilize → seed → water), so a tile's full planting sequence still runs in one stop.
/// </summary>
public static class ManagedActionSweep
{
    public static List<TileAction> Order(
        IReadOnlyList<TileAction> actions,
        TileCoord? start = null,
        Func<TileCoord, bool>? passable = null)
    {
        if (actions.Count <= 1)
            return actions.ToList();

        var sweep = SerpentineSweep.Rank(actions.Select(action => action.Tile), start, passable);
        return actions
            .OrderBy(action => sweep[action.Tile])
            .ThenBy(action => ManagedCropShiftPlan.ActionRank(action.Kind))
            .ThenBy(action => action.ItemId, StringComparer.Ordinal)
            .ToList();
    }
}
