namespace Dayswork.Core.Geometry;

using Dayswork.Core.Domain;

/// <summary>
/// Orders a set of tiles into a boustrophedon (serpentine) field sweep: occupied rows in Y order,
/// X direction alternating per occupied row (occurrence index, not Y parity, so empty rows don't
/// break the chaining). Of the four sweep orientations (top-down/bottom-up × first-row
/// left/right), the one whose first tile is nearest the optional start hint wins, so a worker
/// entering a field from the south sweeps upward instead of walking to the far corner first.
/// Deterministic (top-down, left-first) when no hint is given.
///
/// <para>An optional passability predicate splits each row into contiguous-reachable segments
/// (a row bisected by a pond/fence/building becomes two segments) and serpentines over
/// <em>segments</em> by greedy nearest-end chaining: the worker finishes a segment, then jumps to
/// the nearest end of the next — so it sweeps one side of an obstacle fully before the other,
/// instead of detouring around the gap on every affected row. With no predicate the output is
/// exactly the whole-row sweep (regression-safe default).</para>
/// </summary>
public static class SerpentineSweep
{
    /// <summary>Sweep position per distinct tile; lower rank = visited earlier.</summary>
    public static IReadOnlyDictionary<TileCoord, int> Rank(
        IEnumerable<TileCoord> tiles,
        TileCoord? start = null,
        Func<TileCoord, bool>? passable = null)
    {
        var rows = tiles
            .Distinct()
            .GroupBy(tile => tile.Y)
            .OrderBy(group => group.Key)
            .Select(group => group.OrderBy(tile => tile.X).ToList())
            .ToList();

        var ranks = new Dictionary<TileCoord, int>();
        if (rows.Count == 0)
            return ranks;

        return passable is null
            ? RankWholeRows(rows, start)
            : RankSegments(rows, start, passable);
    }

    private static IReadOnlyDictionary<TileCoord, int> RankWholeRows(
        IReadOnlyList<List<TileCoord>> rows,
        TileCoord? start)
    {
        var ranks = new Dictionary<TileCoord, int>();
        var (topDown, leftFirst) = PickOrientation(rows, start);

        var rank = 0;
        for (var i = 0; i < rows.Count; i++)
        {
            var row = topDown ? rows[i] : rows[rows.Count - 1 - i];
            var leftToRight = (i % 2 == 0) == leftFirst;
            if (leftToRight)
            {
                foreach (var tile in row)
                    ranks[tile] = rank++;
            }
            else
            {
                for (var j = row.Count - 1; j >= 0; j--)
                    ranks[row[j]] = rank++;
            }
        }

        return ranks;
    }

    private static IReadOnlyDictionary<TileCoord, int> RankSegments(
        IReadOnlyList<List<TileCoord>> rows,
        TileCoord? start,
        Func<TileCoord, bool> passable)
    {
        var segments = new List<List<TileCoord>>();
        foreach (var row in rows)
            segments.AddRange(SplitRowIntoSegments(row, passable));

        // Seed the chain from the orientation's starting corner (or the start hint), so a field with
        // no obstacles reproduces today's boustrophedon and one with a hint enters near the worker.
        var (topDown, leftFirst) = PickOrientation(rows, start);
        var seedRow = topDown ? rows[0] : rows[^1];
        var current = leftFirst ? seedRow[0] : seedRow[^1];

        var ranks = new Dictionary<TileCoord, int>();
        var remaining = new List<List<TileCoord>>(segments);
        var rank = 0;

        while (remaining.Count > 0)
        {
            var bestIdx = 0;
            var bestEnterFromStart = true;
            var bestDist = int.MaxValue;

            for (var i = 0; i < remaining.Count; i++)
            {
                var seg = remaining[i];
                var dStart = Manhattan(current, seg[0]);
                var dEnd = Manhattan(current, seg[^1]);
                var enterFromStart = dStart <= dEnd;
                var d = enterFromStart ? dStart : dEnd;

                if (d < bestDist ||
                    (d == bestDist && SegmentPrecedes(seg, remaining[bestIdx])))
                {
                    bestDist = d;
                    bestIdx = i;
                    bestEnterFromStart = enterFromStart;
                }
            }

            var best = remaining[bestIdx];
            if (bestEnterFromStart)
            {
                foreach (var tile in best)
                    ranks[tile] = rank++;
                current = best[^1];
            }
            else
            {
                for (var j = best.Count - 1; j >= 0; j--)
                    ranks[best[j]] = rank++;
                current = best[0];
            }

            remaining.RemoveAt(bestIdx);
        }

        return ranks;
    }

    // A row (sorted by X) splits where the tiles strictly between two work tiles aren't all passable,
    // i.e. the worker would have to detour around an obstacle to get from one to the next.
    private static IEnumerable<List<TileCoord>> SplitRowIntoSegments(
        List<TileCoord> row,
        Func<TileCoord, bool> passable)
    {
        var segment = new List<TileCoord> { row[0] };
        for (var i = 1; i < row.Count; i++)
        {
            if (ReachableBetween(row[i - 1], row[i], passable))
            {
                segment.Add(row[i]);
            }
            else
            {
                yield return segment;
                segment = new List<TileCoord> { row[i] };
            }
        }

        yield return segment;
    }

    private static bool ReachableBetween(TileCoord a, TileCoord b, Func<TileCoord, bool> passable)
    {
        // Same row (a.Y == b.Y), a.X < b.X: every strictly-interior tile must be passable.
        for (var x = a.X + 1; x < b.X; x++)
            if (!passable(new TileCoord(x, a.Y)))
                return false;
        return true;
    }

    private static int Manhattan(TileCoord a, TileCoord b) =>
        Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);

    // Deterministic tie-break: earlier row, then leftmost tile. Each segment is within one row, so
    // seg[0] is (minX, Y).
    private static bool SegmentPrecedes(List<TileCoord> candidate, List<TileCoord> incumbent) =>
        candidate[0].Y != incumbent[0].Y
            ? candidate[0].Y < incumbent[0].Y
            : candidate[0].X < incumbent[0].X;

    private static (bool TopDown, bool LeftFirst) PickOrientation(
        IReadOnlyList<List<TileCoord>> rows,
        TileCoord? start)
    {
        if (start is null)
            return (true, true);

        var topRow = rows[0];
        var bottomRow = rows[^1];
        var variants = new (bool TopDown, bool LeftFirst, TileCoord FirstTile)[]
        {
            (true, true, topRow[0]),
            (true, false, topRow[^1]),
            (false, true, bottomRow[0]),
            (false, false, bottomRow[^1]),
        };

        // Stable OrderBy: on a distance tie the earlier variant (top-down, left-first) wins.
        var best = variants
            .OrderBy(variant =>
                Math.Abs(variant.FirstTile.X - start.Value.X) +
                Math.Abs(variant.FirstTile.Y - start.Value.Y))
            .First();
        return (best.TopDown, best.LeftFirst);
    }
}
