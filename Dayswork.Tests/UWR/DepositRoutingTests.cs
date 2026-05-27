using Dayswork.Core.Domain;
using Dayswork.Core.Shifts;
using Xunit;

namespace Dayswork.Tests.UWR;

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
}
