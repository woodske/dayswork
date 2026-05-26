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
        var walkable = new Dictionary<DestinationKey, (TileCoord Tile, Dictionary<(string ItemId, TaskKind SourceTask, OutputScopeProvenance Provenance), int> Items)>();
        var preMail  = new Dictionary<(string ItemId, TaskKind SourceTask, OutputScopeProvenance Provenance), int>();

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
                    Accumulate(preMail, (item.QualifiedItemId, item.SourceTask, item.Provenance), item.Quantity);
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
        Dictionary<DestinationKey, (TileCoord Tile, Dictionary<(string ItemId, TaskKind SourceTask, OutputScopeProvenance Provenance), int> Items)> walkable,
        DestinationKey dest,
        TileCoord tile,
        BufferedItem item)
    {
        if (!walkable.TryGetValue(dest, out var group))
        {
            group = (tile, new Dictionary<(string ItemId, TaskKind SourceTask, OutputScopeProvenance Provenance), int>());
            walkable[dest] = group;
        }
        Accumulate(group.Items, (item.QualifiedItemId, item.SourceTask, item.Provenance), item.Quantity);
    }

    private static void Accumulate(
        Dictionary<(string ItemId, TaskKind SourceTask, OutputScopeProvenance Provenance), int> map,
        (string ItemId, TaskKind SourceTask, OutputScopeProvenance Provenance) key,
        int qty) =>
        map[key] = map.TryGetValue(key, out var existing) ? existing + qty : qty;

    // Deterministic stack list so planner output is stable regardless of dict order.
    private static IReadOnlyList<RoutedItemStack> ToStacks(
        Dictionary<(string ItemId, TaskKind SourceTask, OutputScopeProvenance Provenance), int> map) =>
        map.OrderBy(kv => kv.Key.ItemId, StringComparer.Ordinal)
           .ThenBy(kv => kv.Key.SourceTask)
           .ThenBy(kv => kv.Key.Provenance.Family)
           .ThenBy(kv => kv.Key.Provenance.ScopeName, StringComparer.Ordinal)
           .Select(kv => new RoutedItemStack(kv.Key.ItemId, kv.Value, kv.Key.SourceTask, kv.Key.Provenance))
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
