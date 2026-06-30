using Dayswork.Core.Domain;
using Dayswork.Core.Shifts;
using Xunit;

namespace Dayswork.Tests.Routing;

public sealed class DepositRoutingTests
{
    [Fact]
    public void TrySelectNearestReachableTile_SelectsReachableAdjacentTile()
    {
        var chest = new TileCoord(10, 10);
        var candidates = new[]
        {
            new TileCoord(chest.X, chest.Y - 1),
            new TileCoord(chest.X + 1, chest.Y),
            new TileCoord(chest.X, chest.Y + 1),
            new TileCoord(chest.X - 1, chest.Y),
        };
        var routeCosts = new Dictionary<TileCoord, int>
        {
            [new TileCoord(chest.X + 1, chest.Y)] = 6,
            [new TileCoord(chest.X - 1, chest.Y)] = 2,
        };

        var found = WorkerRouteSelector.TrySelectNearestReachableTile(
            candidates,
            routeCosts,
            out var standTile);

        Assert.True(found);
        Assert.Equal(new TileCoord(chest.X - 1, chest.Y), standTile);
    }

    [Fact]
    public void TrySelectNearestReachableTile_PreservesStableOrderForEqualCosts()
    {
        var first = new TileCoord(10, 9);
        var second = new TileCoord(11, 10);
        var routeCosts = new Dictionary<TileCoord, int>
        {
            [first] = 4,
            [second] = 4,
        };

        var found = WorkerRouteSelector.TrySelectNearestReachableTile(
            new[] { first, second },
            routeCosts,
            out var standTile);

        Assert.True(found);
        Assert.Equal(first, standTile);
    }

    [Fact]
    public void TrySelectNearestReachableTile_ReturnsFalseWhenNoStandTileIsReachable()
    {
        var candidates = new[]
        {
            new TileCoord(10, 9),
            new TileCoord(11, 10),
            new TileCoord(10, 11),
            new TileCoord(9, 10),
        };

        var found = WorkerRouteSelector.TrySelectNearestReachableTile(
            candidates,
            new Dictionary<TileCoord, int>(),
            out _);

        Assert.False(found);
    }

    // One orthogonal and one diagonal stand for the same target; the diagonal carries a +1 penalty.
    private static readonly StandTile Orthogonal = new(new TileCoord(10, 9), false);
    private static readonly StandTile Diagonal = new(new TileCoord(9, 9), true);

    [Fact]
    public void TrySelectPreferredStandTile_PrefersOrthogonalWhenCostsEqual()
    {
        var routeCosts = new Dictionary<TileCoord, int>
        {
            [Orthogonal.Tile] = 4,
            [Diagonal.Tile] = 4,
        };

        var found = WorkerRouteSelector.TrySelectPreferredStandTile(
            new[] { Orthogonal, Diagonal }, routeCosts, out var standTile);

        Assert.True(found);
        Assert.Equal(Orthogonal.Tile, standTile);
    }

    [Fact]
    public void TrySelectPreferredStandTile_PrefersOrthogonalWhenOneTileFarther()
    {
        // Orthogonal is one tile farther in raw travel, but the +1 diagonal penalty ties them and
        // the orthogonal tile (listed first) wins.
        var routeCosts = new Dictionary<TileCoord, int>
        {
            [Orthogonal.Tile] = 5,
            [Diagonal.Tile] = 4,
        };

        var found = WorkerRouteSelector.TrySelectPreferredStandTile(
            new[] { Orthogonal, Diagonal }, routeCosts, out var standTile);

        Assert.True(found);
        Assert.Equal(Orthogonal.Tile, standTile);
    }

    [Fact]
    public void TrySelectPreferredStandTile_UsesDiagonalWhenOrthogonalIsTwoTilesFarther()
    {
        var routeCosts = new Dictionary<TileCoord, int>
        {
            [Orthogonal.Tile] = 6,
            [Diagonal.Tile] = 4,
        };

        var found = WorkerRouteSelector.TrySelectPreferredStandTile(
            new[] { Orthogonal, Diagonal }, routeCosts, out var standTile);

        Assert.True(found);
        Assert.Equal(Diagonal.Tile, standTile);
    }

    [Fact]
    public void TrySelectPreferredStandTile_UsesDiagonalWhenNoOrthogonalReachable()
    {
        var routeCosts = new Dictionary<TileCoord, int>
        {
            [Diagonal.Tile] = 9,
        };

        var found = WorkerRouteSelector.TrySelectPreferredStandTile(
            new[] { Orthogonal, Diagonal }, routeCosts, out var standTile);

        Assert.True(found);
        Assert.Equal(Diagonal.Tile, standTile);
    }

    [Fact]
    public void TrySelectPreferredStandTile_ReturnsFalseWhenNothingReachable()
    {
        var found = WorkerRouteSelector.TrySelectPreferredStandTile(
            new[] { Orthogonal, Diagonal },
            new Dictionary<TileCoord, int>(),
            out _);

        Assert.False(found);
    }
}
