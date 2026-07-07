using Dayswork.Core.Domain;

namespace Dayswork.Core.Pathing;

/// <summary>
/// Pure breadth-first pathfinding over an <see cref="IPassabilityView"/>. Lifted verbatim (same
/// semantics) out of <c>WorkerMovementDriver</c> so it is unit-testable and can run against either
/// a cached <see cref="PassabilityGrid"/> (route-cost selection) or a live probe view (navigation
/// fallback). Unweighted, 4-directional.
///
/// <para><b>Tie-break order is load-bearing.</b> The cardinal neighbour order is N, E, S, W and
/// must not change — both route selectors are first-wins-on-ties over the resulting cost map, so
/// reordering neighbours silently changes which work tile the worker picks (see the
/// worker-action-adjacency notes).</para>
/// </summary>
public static class GridPathfinder
{
    /// <summary>
    /// BFS flood fill: for every tile reachable from <paramref name="source"/>, its unweighted
    /// step distance. The source is always present at cost 0 (its own passability is never
    /// checked — the worker may legitimately stand on a technically-blocked tile).
    /// </summary>
    public static IReadOnlyDictionary<TileCoord, int> ComputeRouteCosts(IPassabilityView view, TileCoord source)
    {
        var queue = new Queue<TileCoord>();
        var visited = new HashSet<TileCoord> { source };
        var routeCosts = new Dictionary<TileCoord, int> { [source] = 0 };

        queue.Enqueue(source);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            var currentCost = routeCosts[current];

            foreach (var next in Neighbours(current))
            {
                if (!visited.Add(next) ||
                    !InBounds(view, next) ||
                    !view.IsPassable(next.X, next.Y))
                    continue;

                routeCosts[next] = currentCost + 1;
                queue.Enqueue(next);
            }
        }

        return routeCosts;
    }

    /// <summary>
    /// BFS shortest route from <paramref name="start"/> to <paramref name="end"/>. The returned
    /// route excludes the start tile and ends with <paramref name="end"/> (an empty route means
    /// start == end). The start tile's passability is never checked; the end tile must be passable.
    /// </summary>
    public static bool TryFindRoute(IPassabilityView view, TileCoord start, TileCoord end, out IReadOnlyList<TileCoord> route)
    {
        if (start == end)
        {
            route = Array.Empty<TileCoord>();
            return true;
        }

        if (!InBounds(view, end) || !view.IsPassable(end.X, end.Y))
        {
            route = Array.Empty<TileCoord>();
            return false;
        }

        var queue = new Queue<TileCoord>();
        var cameFrom = new Dictionary<TileCoord, TileCoord?> { [start] = null };
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current == end)
                break;

            foreach (var next in Neighbours(current))
            {
                if (cameFrom.ContainsKey(next) ||
                    !InBounds(view, next) ||
                    !view.IsPassable(next.X, next.Y))
                    continue;

                cameFrom[next] = current;
                queue.Enqueue(next);
            }
        }

        if (!cameFrom.ContainsKey(end))
        {
            route = Array.Empty<TileCoord>();
            return false;
        }

        route = ReconstructPath(end, cameFrom);
        return true;
    }

    private static bool InBounds(IPassabilityView view, TileCoord t) =>
        t.X >= 0 && t.Y >= 0 && t.X < view.Width && t.Y < view.Height;

    private static IEnumerable<TileCoord> Neighbours(TileCoord tile)
    {
        yield return new TileCoord(tile.X, tile.Y - 1); // N
        yield return new TileCoord(tile.X + 1, tile.Y); // E
        yield return new TileCoord(tile.X, tile.Y + 1); // S
        yield return new TileCoord(tile.X - 1, tile.Y); // W
    }

    private static IReadOnlyList<TileCoord> ReconstructPath(TileCoord end, Dictionary<TileCoord, TileCoord?> cameFrom)
    {
        var route = new Stack<TileCoord>();
        var current = end;
        while (cameFrom[current] is { } previous)
        {
            route.Push(current);
            current = previous;
        }

        return route.ToList();
    }
}
