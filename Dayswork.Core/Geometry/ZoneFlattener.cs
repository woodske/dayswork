namespace Dayswork.Core.Geometry;

using Dayswork.Core.Domain;

/// <summary>
/// Merges a set of axis-aligned <see cref="Zone"/> rectangles into the fewest rectangles that cover
/// the same tiles. Touching, co-aligned rectangles collapse together (a contiguous block becomes one
/// zone); shapes whose union is not itself a rectangle (L-shapes, corner-only touches) stay split
/// because the model can only represent rectangles.
/// </summary>
public static class ZoneFlattener
{
    /// <summary>
    /// Algorithm: per <see cref="Zone.LocationName"/>, expand to a unique tile set, build maximal
    /// horizontal runs row by row, then merge a run into the row above when an identical
    /// <c>[x1..x2]</c> run sits directly above it. Output is deterministic, sorted by (Y, then X),
    /// and grouped by location in ordinal order.
    /// </summary>
    public static List<Zone> Flatten(IReadOnlyList<Zone> zones)
    {
        var byLocation = new Dictionary<string, HashSet<(int X, int Y)>>(StringComparer.Ordinal);
        foreach (var zone in zones)
        {
            if (!byLocation.TryGetValue(zone.LocationName, out var tiles))
                byLocation[zone.LocationName] = tiles = new HashSet<(int, int)>();

            for (var y = zone.TopLeft.Y; y <= zone.BottomRight.Y; y++)
            for (var x = zone.TopLeft.X; x <= zone.BottomRight.X; x++)
                tiles.Add((x, y));
        }

        var result = new List<Zone>();
        foreach (var locationName in byLocation.Keys.OrderBy(name => name, StringComparer.Ordinal))
            result.AddRange(FlattenLocation(locationName, byLocation[locationName]));

        return result;
    }

    private static IEnumerable<Zone> FlattenLocation(string locationName, HashSet<(int X, int Y)> tiles)
    {
        // Maximal horizontal runs per row, keyed by row. Each run is an inclusive [x1, x2] span.
        var runsByRow = new Dictionary<int, List<(int X1, int X2)>>();
        foreach (var y in tiles.Select(t => t.Y).Distinct())
        {
            var xs = tiles.Where(t => t.Y == y).Select(t => t.X).OrderBy(x => x).ToList();
            var runs = new List<(int X1, int X2)>();
            var start = xs[0];
            var prev = xs[0];
            for (var i = 1; i < xs.Count; i++)
            {
                if (xs[i] == prev + 1) { prev = xs[i]; continue; }
                runs.Add((start, prev));
                start = prev = xs[i];
            }
            runs.Add((start, prev));
            runsByRow[y] = runs;
        }

        // Open rectangles keyed by their [x1, x2] span; tracks the run's vertical extent so a run that
        // continues directly below extends the same rectangle instead of starting a new one.
        var open = new Dictionary<(int X1, int X2), (int Top, int Bottom)>();
        var completed = new List<Zone>();

        foreach (var y in runsByRow.Keys.OrderBy(v => v))
        {
            var spans = runsByRow[y].ToHashSet();

            // Close rectangles whose span is absent in this row or that this row doesn't continue.
            foreach (var span in open.Keys.ToList())
            {
                var (top, bottom) = open[span];
                if (spans.Contains(span) && bottom == y - 1)
                    continue;   // still growing — handled below
                completed.Add(new Zone(locationName, new TileCoord(span.X1, top), new TileCoord(span.X2, bottom)));
                open.Remove(span);
            }

            foreach (var span in runsByRow[y])
            {
                if (open.TryGetValue(span, out var extent))
                    open[span] = (extent.Top, y);   // extend downward
                else
                    open[span] = (y, y);            // start a new rectangle
            }
        }

        foreach (var (span, extent) in open)
            completed.Add(new Zone(locationName, new TileCoord(span.X1, extent.Top), new TileCoord(span.X2, extent.Bottom)));

        return completed
            .OrderBy(z => z.TopLeft.Y)
            .ThenBy(z => z.TopLeft.X);
    }
}
