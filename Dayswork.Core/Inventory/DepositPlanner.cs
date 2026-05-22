using Dayswork.Core.Domain;

namespace Dayswork.Core.Inventory;

// C-11 DepositPlanner — pure, zero Stardew refs (Pattern M / MAINT-U14-01).
public sealed class DepositPlanner : IDepositPlanner
{
    public DepositPlan Plan(
        IReadOnlyList<BufferedItem> snapshot,
        IReadOnlyDictionary<TaskKind, DestinationKey> assignments,
        TileCoord shippingBinTile,
        TileCoord workerStart,
        Func<TileCoord, TileCoord, int> distance)
    {
        // Walkable destinations grouped by key → (representative tile, item id → qty).
        var walkable = new Dictionary<DestinationKey, (TileCoord Tile, Dictionary<string, int> Items)>();
        var preMail  = new Dictionary<string, int>();

        foreach (var item in snapshot)
        {
            var dest = ResolveDestination(item.SourceTask, assignments);
            switch (dest)
            {
                case ChestDestination chest:
                    AddToGroup(walkable, dest, chest.Ref.Tile, item);
                    break;
                case ShippingBinDestination:
                    AddToGroup(walkable, dest, shippingBinTile, item);
                    break;
                default: // MailDestination or unresolved ⇒ mail next morning (FD-Q2=A / FR-OUT-04)
                    Accumulate(preMail, item.QualifiedItemId, item.Quantity);
                    break;
            }
        }

        var unordered = walkable
            .Select(kv => new DepositTrip(kv.Key, kv.Value.Tile, ToStacks(kv.Value.Items)))
            .ToList();

        var ordered = OrderNearestNeighbor(unordered, workerStart, distance);

        return new DepositPlan(ordered, ToStacks(preMail));
    }

    private static DestinationKey ResolveDestination(
        TaskKind task,
        IReadOnlyDictionary<TaskKind, DestinationKey> assignments) =>
        assignments.TryGetValue(task, out var dest) && dest is not null
            ? dest
            : MailDestination.Instance;

    private static void AddToGroup(
        Dictionary<DestinationKey, (TileCoord Tile, Dictionary<string, int> Items)> walkable,
        DestinationKey dest,
        TileCoord tile,
        BufferedItem item)
    {
        if (!walkable.TryGetValue(dest, out var group))
        {
            group = (tile, new Dictionary<string, int>());
            walkable[dest] = group;
        }
        Accumulate(group.Items, item.QualifiedItemId, item.Quantity);
    }

    private static void Accumulate(Dictionary<string, int> map, string id, int qty) =>
        map[id] = map.TryGetValue(id, out var existing) ? existing + qty : qty;

    // Deterministic stack list (sorted by id) so planner output is stable regardless of dict order.
    private static IReadOnlyList<ItemStack> ToStacks(Dictionary<string, int> map) =>
        map.OrderBy(kv => kv.Key, StringComparer.Ordinal)
           .Select(kv => new ItemStack(kv.Key, kv.Value))
           .ToList();

    private static IReadOnlyList<DepositTrip> OrderNearestNeighbor(
        List<DepositTrip> trips,
        TileCoord start,
        Func<TileCoord, TileCoord, int> distance)
    {
        var remaining = new List<DepositTrip>(trips);
        var ordered   = new List<DepositTrip>(trips.Count);
        var current   = start;

        while (remaining.Count > 0)
        {
            int bestIdx  = 0;
            int bestDist = distance(current, remaining[0].Tile);
            for (int i = 1; i < remaining.Count; i++)
            {
                int d = distance(current, remaining[i].Tile);
                if (d < bestDist)
                {
                    bestDist = d;
                    bestIdx  = i;
                }
            }

            var next = remaining[bestIdx];
            ordered.Add(next);
            current = next.Tile;
            remaining.RemoveAt(bestIdx);
        }

        return ordered;
    }
}
