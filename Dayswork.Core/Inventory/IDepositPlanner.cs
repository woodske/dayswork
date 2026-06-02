using Dayswork.Core.Domain;

namespace Dayswork.Core.Inventory;

public interface IDepositPlanner
{
    // Pure planning: resolve each buffered item's destination, group+consolidate into one trip
    // per walkable destination, order trips nearest-neighbor from workerStart, and surface
    // automatic-overflow items separately. The distance oracle keeps Core free of game pathfinding.
    DepositPlan Plan(
        IReadOnlyList<BufferedItem> snapshot,
        IReadOnlyDictionary<TaskKind, DestinationKey> assignments,
        TileCoord shippingBinTile,
        TileCoord workerStart,
        Func<TileCoord, TileCoord, int> distance);
}
