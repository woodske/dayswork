namespace Dayswork.Core.Pathing;

/// <summary>
/// A read-only passability lookup over a rectangular tile grid, consumed by
/// <see cref="GridPathfinder"/>. Two implementations exist (the ceremony is earned):
/// <see cref="PassabilityGrid"/> — a precomputed, cacheable snapshot used for the hot
/// route-cost selection path; and a game-side live-probe view used for the rare navigation
/// fallback that must never trust a stale cache.
/// </summary>
public interface IPassabilityView
{
    int Width { get; }

    int Height { get; }

    /// <summary>
    /// Whether a worker may stand on / route through the tile. Callers guarantee the tile is in
    /// bounds (<c>0 &lt;= x &lt; Width</c>, <c>0 &lt;= y &lt; Height</c>) before calling — the
    /// pathfinder bounds-checks first, so implementations need not.
    /// </summary>
    bool IsPassable(int x, int y);
}
