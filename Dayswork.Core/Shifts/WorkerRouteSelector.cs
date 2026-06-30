using Dayswork.Core.Domain;

namespace Dayswork.Core.Shifts;

/// <summary>
/// Selects the best already-evaluated route candidate for one active worker batch.
/// The selector is pure: callers own candidate discovery, pathing, and dispatch.
/// </summary>
public sealed class WorkerRouteSelector
{
    // Player-ordered category priority is authoritative: pick the highest-priority category that
    // still has reachable work, then the nearest candidate within it. Route cost breaks ties only
    // among same-category candidates (which share a PriorityRank), giving strict ordering between
    // categories and nearest-first within a category.
    public WorkerRouteCandidate? Select(IEnumerable<WorkerRouteCandidate> candidates) =>
        candidates
            .Where(candidate => candidate.Reachable)
            .OrderBy(candidate => candidate.PriorityRank)
            .ThenBy(candidate => candidate.RouteCost)
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

    // The worker can stand orthogonally or diagonally adjacent to a target (player parity), but an
    // orthogonal stand looks more natural — so diagonal candidates carry a +1 travel-cost penalty.
    // Effect: pick the nearest reachable stand, but only take a diagonal when it's 2+ tiles closer
    // than the nearest orthogonal one (or when no orthogonal stand is reachable). Cardinals are
    // listed first, so the strict `<` keeps an orthogonal tile on an effective-cost tie.
    private const int DiagonalTravelPenalty = 1;

    public static bool TrySelectPreferredStandTile(
        IEnumerable<StandTile> candidates,
        IReadOnlyDictionary<TileCoord, int> routeCosts,
        out TileCoord tile)
    {
        var bestCost = int.MaxValue;
        TileCoord? bestTile = null;

        foreach (var candidate in candidates.Distinct())
        {
            if (!routeCosts.TryGetValue(candidate.Tile, out var cost))
                continue;

            var effectiveCost = cost + (candidate.Diagonal ? DiagonalTravelPenalty : 0);
            if (effectiveCost >= bestCost)
                continue;

            bestCost = effectiveCost;
            bestTile = candidate.Tile;
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
