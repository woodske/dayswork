namespace Dayswork.Tests.Geometry;

using Dayswork.Core.Domain;
using Dayswork.Core.Geometry;
using Xunit;

public class ZoneFlattenerTests
{
    private static Zone Z(int x1, int y1, int x2, int y2, string loc = "Farm") =>
        new(loc, new TileCoord(x1, y1), new TileCoord(x2, y2));

    private static HashSet<(int, int)> TilesOf(IEnumerable<Zone> zones)
    {
        var set = new HashSet<(int, int)>();
        foreach (var z in zones)
            for (var x = z.TopLeft.X; x <= z.BottomRight.X; x++)
            for (var y = z.TopLeft.Y; y <= z.BottomRight.Y; y++)
                set.Add((x, y));
        return set;
    }

    [Fact]
    public void HorizontallyAdjacentSameHeight_MergeToOne()
    {
        var input = new[] { Z(0, 0, 2, 1), Z(3, 0, 5, 1), Z(6, 0, 8, 1) };
        var result = ZoneFlattener.Flatten(input);
        Assert.Single(result);
        Assert.Equal(Z(0, 0, 8, 1), result[0]);
    }

    [Fact]
    public void VerticallyStackedSameWidth_MergeToOne()
    {
        var input = new[] { Z(0, 0, 2, 0), Z(0, 1, 2, 1), Z(0, 2, 2, 2) };
        var result = ZoneFlattener.Flatten(input);
        Assert.Single(result);
        Assert.Equal(Z(0, 0, 2, 2), result[0]);
    }

    [Fact]
    public void TwoByTwoBlockOfRects_MergeToOne()
    {
        var input = new[] { Z(0, 0, 1, 1), Z(2, 0, 3, 1), Z(0, 2, 1, 3), Z(2, 2, 3, 3) };
        var result = ZoneFlattener.Flatten(input);
        Assert.Single(result);
        Assert.Equal(Z(0, 0, 3, 3), result[0]);
    }

    [Fact]
    public void OverlappingRects_MergeAndPreserveTiles()
    {
        var input = new[] { Z(0, 0, 2, 2), Z(1, 1, 3, 3) };
        var result = ZoneFlattener.Flatten(input);
        Assert.Equal(TilesOf(input), TilesOf(result));
        // Union is an L/staircase shape — not a single rectangle.
        Assert.True(result.Count >= 2);
    }

    [Fact]
    public void LShape_StaysSplit_AndPreservesTiles()
    {
        // 3x3 block plus a one-tile nub to the right of the top row.
        var input = new[] { Z(0, 0, 2, 2), Z(3, 0, 4, 0) };
        var result = ZoneFlattener.Flatten(input);
        Assert.Equal(2, result.Count);
        Assert.Equal(TilesOf(input), TilesOf(result));
    }

    [Fact]
    public void DisjointRects_StaySeparate()
    {
        var input = new[] { Z(0, 0, 1, 1), Z(10, 10, 11, 11) };
        var result = ZoneFlattener.Flatten(input);
        Assert.Equal(2, result.Count);
        Assert.Equal(TilesOf(input), TilesOf(result));
    }

    [Fact]
    public void CornerTouchingRects_DoNotMerge()
    {
        var input = new[] { Z(0, 0, 1, 1), Z(2, 2, 3, 3) };
        var result = ZoneFlattener.Flatten(input);
        Assert.Equal(2, result.Count);
        Assert.Equal(TilesOf(input), TilesOf(result));
    }

    [Fact]
    public void DifferentLocations_StaySeparate()
    {
        var input = new[] { Z(0, 0, 2, 1, "Farm"), Z(0, 0, 2, 1, "Greenhouse") };
        var result = ZoneFlattener.Flatten(input);
        Assert.Equal(2, result.Count);
        Assert.Contains(result, z => z.LocationName == "Farm");
        Assert.Contains(result, z => z.LocationName == "Greenhouse");
    }

    [Fact]
    public void Output_IsDeterministic_AndSorted()
    {
        var input = new[] { Z(5, 5, 6, 6), Z(0, 0, 1, 1), Z(0, 5, 1, 6) };
        var a = ZoneFlattener.Flatten(input);
        var b = ZoneFlattener.Flatten(input);
        Assert.Equal(a, b);
        // Sorted by TopLeft.Y then TopLeft.X.
        for (var i = 1; i < a.Count; i++)
        {
            var prev = a[i - 1];
            var cur = a[i];
            Assert.True(prev.TopLeft.Y < cur.TopLeft.Y
                || (prev.TopLeft.Y == cur.TopLeft.Y && prev.TopLeft.X <= cur.TopLeft.X));
        }
    }

    [Fact]
    public void EmptyInput_ReturnsEmpty()
    {
        Assert.Empty(ZoneFlattener.Flatten(System.Array.Empty<Zone>()));
    }

    [Fact]
    public void Idempotent_FlatteningTwiceMatchesOnce()
    {
        var input = new[] { Z(0, 0, 2, 1), Z(3, 0, 5, 1) };
        var once = ZoneFlattener.Flatten(input);
        var twice = ZoneFlattener.Flatten(once);
        Assert.Equal(once, twice);
    }
}
