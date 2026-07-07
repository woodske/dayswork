using Dayswork.Core.Domain;
using Dayswork.Core.Pathing;
using Xunit;

namespace Dayswork.Tests.Pathing;

/// <summary>
/// GridPathfinder BFS behaviour — cost maps, route reconstruction, unreachable targets, and the
/// load-bearing N,E,S,W tie-break order. This algorithm was untested while it lived inside
/// WorkerMovementDriver; these lock its semantics so the extraction can't drift.
/// </summary>
public sealed class GridPathfinderTests
{
    /// <summary>Build a grid from ASCII rows: '.' passable, '#' blocked. Row 0 is y=0.</summary>
    private static PassabilityGrid Grid(params string[] rows)
    {
        var height = rows.Length;
        var width = rows[0].Length;
        var passable = new bool[width, height];
        for (var y = 0; y < height; y++)
        {
            Assert.Equal(width, rows[y].Length); // rectangular
            for (var x = 0; x < width; x++)
                passable[x, y] = rows[y][x] != '#';
        }

        return new PassabilityGrid(passable);
    }

    [Fact]
    public void ComputeRouteCosts_OpenGrid_ManhattanDistances()
    {
        var grid = Grid(
            "...",
            "...",
            "...");

        var costs = GridPathfinder.ComputeRouteCosts(grid, new TileCoord(0, 0));

        Assert.Equal(0, costs[new TileCoord(0, 0)]);
        Assert.Equal(1, costs[new TileCoord(1, 0)]);
        Assert.Equal(1, costs[new TileCoord(0, 1)]);
        Assert.Equal(2, costs[new TileCoord(1, 1)]);
        Assert.Equal(4, costs[new TileCoord(2, 2)]);
        Assert.Equal(9, costs.Count); // every tile reachable
    }

    [Fact]
    public void ComputeRouteCosts_SourceAlwaysPresentEvenIfBlocked()
    {
        // Source tile itself is impassable (worker may stand on a technically-blocked tile),
        // but the flood fill still starts there at cost 0 and explores passable neighbours.
        var grid = Grid(
            "#..",
            "...");

        var costs = GridPathfinder.ComputeRouteCosts(grid, new TileCoord(0, 0));

        Assert.Equal(0, costs[new TileCoord(0, 0)]);
        Assert.Equal(1, costs[new TileCoord(1, 0)]);
        Assert.Equal(1, costs[new TileCoord(0, 1)]);
    }

    [Fact]
    public void ComputeRouteCosts_WalledOffRegionIsUnreachable()
    {
        // A full vertical wall at x=1 seals off column x=2.
        var grid = Grid(
            ".#.",
            ".#.",
            ".#.");

        var costs = GridPathfinder.ComputeRouteCosts(grid, new TileCoord(0, 0));

        Assert.True(costs.ContainsKey(new TileCoord(0, 2)));
        Assert.False(costs.ContainsKey(new TileCoord(2, 0)));
        Assert.False(costs.ContainsKey(new TileCoord(1, 0))); // the wall itself
    }

    [Fact]
    public void TryFindRoute_StartEqualsEnd_EmptyRouteSucceeds()
    {
        var grid = Grid("..", "..");

        var ok = GridPathfinder.TryFindRoute(grid, new TileCoord(1, 1), new TileCoord(1, 1), out var route);

        Assert.True(ok);
        Assert.Empty(route);
    }

    [Fact]
    public void TryFindRoute_ExcludesStartIncludesEnd()
    {
        var grid = Grid("...");

        var ok = GridPathfinder.TryFindRoute(grid, new TileCoord(0, 0), new TileCoord(2, 0), out var route);

        Assert.True(ok);
        Assert.Equal(new[] { new TileCoord(1, 0), new TileCoord(2, 0) }, route);
    }

    [Fact]
    public void TryFindRoute_BlockedEndTile_Fails()
    {
        var grid = Grid("..#");

        var ok = GridPathfinder.TryFindRoute(grid, new TileCoord(0, 0), new TileCoord(2, 0), out var route);

        Assert.False(ok);
        Assert.Empty(route);
    }

    [Fact]
    public void TryFindRoute_UnreachableEnd_Fails()
    {
        var grid = Grid(
            ".#.",
            ".#.",
            ".#.");

        var ok = GridPathfinder.TryFindRoute(grid, new TileCoord(0, 0), new TileCoord(2, 2), out var route);

        Assert.False(ok);
        Assert.Empty(route);
    }

    [Fact]
    public void TryFindRoute_OutOfBoundsEnd_Fails()
    {
        var grid = Grid("..", "..");

        var ok = GridPathfinder.TryFindRoute(grid, new TileCoord(0, 0), new TileCoord(5, 5), out var route);

        Assert.False(ok);
        Assert.Empty(route);
    }

    [Fact]
    public void TryFindRoute_RoutesAroundObstacle()
    {
        // Wall forces a detour: (0,0) -> down around the '#' at (1,0)/(1,1) -> (2,0).
        var grid = Grid(
            ".#.",
            "...");

        var ok = GridPathfinder.TryFindRoute(grid, new TileCoord(0, 0), new TileCoord(2, 0), out var route);

        Assert.True(ok);
        Assert.Equal(new TileCoord(2, 0), route[^1]);
        // Every step is a cardinal move and passable.
        var prev = new TileCoord(0, 0);
        foreach (var step in route)
        {
            Assert.Equal(1, Math.Abs(step.X - prev.X) + Math.Abs(step.Y - prev.Y));
            Assert.True(grid.IsPassable(step));
            prev = step;
        }
    }

    [Fact]
    public void Neighbours_TieBreak_NorthBeforeEast()
    {
        // From (1,1) both N (1,0) and E (2,1) are one step away and open. BFS enqueues N first,
        // so the recorded cameFrom for a tile equidistant via either must reflect N-first order.
        // Concretely: the route to (2,0) prefers going N then E (the "up then right" staircase),
        // never E then N — proving neighbour order N,E,S,W is preserved.
        var grid = Grid(
            "...",
            "...",
            "...");

        var ok = GridPathfinder.TryFindRoute(grid, new TileCoord(1, 1), new TileCoord(2, 0), out var route);

        Assert.True(ok);
        // N-first means the first step is North (1,0), not East (2,1).
        Assert.Equal(new TileCoord(1, 0), route[0]);
        Assert.Equal(new[] { new TileCoord(1, 0), new TileCoord(2, 0) }, route);
    }

    [Fact]
    public void ComputeRouteCosts_IsDeterministic()
    {
        var grid = Grid(
            "....#",
            ".##..",
            ".....",
            "#..#.");

        var a = GridPathfinder.ComputeRouteCosts(grid, new TileCoord(0, 0));
        var b = GridPathfinder.ComputeRouteCosts(grid, new TileCoord(0, 0));

        Assert.Equal(a.Count, b.Count);
        foreach (var kv in a)
            Assert.Equal(kv.Value, b[kv.Key]);
    }

    [Fact]
    public void PassabilityGrid_SetPassable_ReprobesSingleCell()
    {
        var grid = Grid("..", "..");
        Assert.True(grid.IsPassable(new TileCoord(1, 0)));

        grid.SetPassable(1, 0, false);
        Assert.False(grid.IsPassable(new TileCoord(1, 0)));

        // Out-of-bounds writes are ignored (no throw).
        grid.SetPassable(99, 99, true);
    }
}
