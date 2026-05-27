using Dayswork.Core.Domain;

namespace Dayswork.Core.Shifts;

/// <summary>
/// Selects the best already-evaluated route candidate for one active worker batch.
/// The selector is pure: callers own candidate discovery, pathing, and dispatch.
/// </summary>
public sealed class WorkerRouteSelector
{
    public WorkerRouteCandidate? Select(IEnumerable<WorkerRouteCandidate> candidates) =>
        candidates
            .Where(candidate => candidate.Reachable)
            .OrderBy(candidate => candidate.RouteCost)
            .ThenBy(candidate => candidate.PriorityRank)
            .ThenBy(candidate => candidate.StableOrder)
            .FirstOrDefault();

    public static bool TrySelectNearestReachableTile(
        IEnumerable<TileCoord> candidates,
        IReadOnlyDictionary<TileCoord, int> routeCosts,
        out TileCoord tile)
    {
        var bestCost = int.MaxValue;
        TileCoord? bestTile = null;

        foreach (var candidate in candidates.Distinct())
        {
            if (!routeCosts.TryGetValue(candidate, out var cost) || cost >= bestCost)
                continue;

            bestCost = cost;
            bestTile = candidate;
        }

        tile = bestTile ?? default;
        return bestTile is not null;
    }
}

public sealed record WorkerRouteCandidate(
    int CandidateId,
    TaskKind Task,
    int PriorityRank,
    int StableOrder,
    TileCoord InteractionTile,
    bool Reachable,
    int RouteCost);
