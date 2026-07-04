using Dayswork.Core.Domain;
using Dayswork.Core.Geometry;
using Xunit;

namespace Dayswork.Tests.Geometry;

public sealed class SerpentineSweepTests
{
    [Fact]
    public void Dense_rectangle_sweeps_boustrophedon_from_top_left_by_default()
    {
        var tiles = Grid(x0: 0, x1: 2, y0: 0, y1: 1);

        var ranks = SerpentineSweep.Rank(tiles);

        Assert.Equal(0, ranks[new TileCoord(0, 0)]);
        Assert.Equal(1, ranks[new TileCoord(1, 0)]);
        Assert.Equal(2, ranks[new TileCoord(2, 0)]);
        Assert.Equal(3, ranks[new TileCoord(2, 1)]);
        Assert.Equal(4, ranks[new TileCoord(1, 1)]);
        Assert.Equal(5, ranks[new TileCoord(0, 1)]);
    }

    [Fact]
    public void Direction_alternates_per_occupied_row_across_gaps()
    {
        // Rows 0 and 5 with nothing between: the second occupied row still runs right-to-left so
        // the sweep chains end-to-end instead of resetting on Y parity.
        var tiles = new[]
        {
            new TileCoord(0, 0), new TileCoord(1, 0),
            new TileCoord(0, 5), new TileCoord(1, 5),
        };

        var ranks = SerpentineSweep.Rank(tiles);

        Assert.Equal(0, ranks[new TileCoord(0, 0)]);
        Assert.Equal(1, ranks[new TileCoord(1, 0)]);
        Assert.Equal(2, ranks[new TileCoord(1, 5)]);
        Assert.Equal(3, ranks[new TileCoord(0, 5)]);
    }

    [Fact]
    public void Holes_in_a_row_keep_x_order_within_the_direction()
    {
        var tiles = new[] { new TileCoord(7, 3), new TileCoord(0, 3), new TileCoord(3, 3) };

        var ranks = SerpentineSweep.Rank(tiles);

        Assert.Equal(0, ranks[new TileCoord(0, 3)]);
        Assert.Equal(1, ranks[new TileCoord(3, 3)]);
        Assert.Equal(2, ranks[new TileCoord(7, 3)]);
    }

    [Fact]
    public void Start_near_bottom_right_sweeps_bottom_up_starting_right()
    {
        var tiles = Grid(x0: 0, x1: 2, y0: 0, y1: 2);

        var ranks = SerpentineSweep.Rank(tiles, start: new TileCoord(10, 10));

        Assert.Equal(0, ranks[new TileCoord(2, 2)]);
        Assert.Equal(1, ranks[new TileCoord(1, 2)]);
        Assert.Equal(2, ranks[new TileCoord(0, 2)]);
        Assert.Equal(3, ranks[new TileCoord(0, 1)]);
        Assert.Equal(8, ranks[new TileCoord(0, 0)]);
    }

    [Fact]
    public void Start_near_top_right_starts_right_on_the_top_row()
    {
        var tiles = Grid(x0: 0, x1: 2, y0: 0, y1: 1);

        var ranks = SerpentineSweep.Rank(tiles, start: new TileCoord(5, 0));

        Assert.Equal(0, ranks[new TileCoord(2, 0)]);
        Assert.Equal(1, ranks[new TileCoord(1, 0)]);
        Assert.Equal(2, ranks[new TileCoord(0, 0)]);
        Assert.Equal(3, ranks[new TileCoord(0, 1)]);
        Assert.Equal(5, ranks[new TileCoord(2, 1)]);
    }

    [Fact]
    public void Every_tile_gets_a_distinct_contiguous_rank()
    {
        var tiles = Grid(x0: 3, x1: 8, y0: 2, y1: 6);

        var ranks = SerpentineSweep.Rank(tiles, start: new TileCoord(4, 9));

        Assert.Equal(tiles.Count, ranks.Count);
        Assert.Equal(Enumerable.Range(0, tiles.Count), ranks.Values.OrderBy(v => v));
    }

    [Fact]
    public void Duplicate_input_tiles_are_tolerated()
    {
        var tiles = new[] { new TileCoord(1, 1), new TileCoord(1, 1), new TileCoord(2, 1) };

        var ranks = SerpentineSweep.Rank(tiles);

        Assert.Equal(2, ranks.Count);
        Assert.Equal(0, ranks[new TileCoord(1, 1)]);
        Assert.Equal(1, ranks[new TileCoord(2, 1)]);
    }

    [Fact]
    public void Empty_input_returns_empty()
    {
        Assert.Empty(SerpentineSweep.Rank(Array.Empty<TileCoord>()));
    }

    private static List<TileCoord> Grid(int x0, int x1, int y0, int y1)
    {
        var tiles = new List<TileCoord>();
        for (var y = y0; y <= y1; y++)
        for (var x = x0; x <= x1; x++)
            tiles.Add(new TileCoord(x, y));
        return tiles;
    }
}
