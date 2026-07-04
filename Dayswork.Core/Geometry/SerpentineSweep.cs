namespace Dayswork.Core.Geometry;

using Dayswork.Core.Domain;

/// <summary>
/// Orders a set of tiles into a boustrophedon (serpentine) field sweep: occupied rows in Y order,
/// X direction alternating per occupied row (occurrence index, not Y parity, so empty rows don't
/// break the chaining). Of the four sweep orientations (top-down/bottom-up × first-row
/// left/right), the one whose first tile is nearest the optional start hint wins, so a worker
/// entering a field from the south sweeps upward instead of walking to the far corner first.
/// Deterministic (top-down, left-first) when no hint is given.
/// </summary>
public static class SerpentineSweep
{
    /// <summary>Sweep position per distinct tile; lower rank = visited earlier.</summary>
    public static IReadOnlyDictionary<TileCoord, int> Rank(IEnumerable<TileCoord> tiles, TileCoord? start = null)
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
