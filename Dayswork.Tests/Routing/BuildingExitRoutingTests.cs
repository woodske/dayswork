using Dayswork.Core.Domain;
using Dayswork.Orchestration;
using Xunit;

namespace Dayswork.Tests.Routing;

public sealed class BuildingExitRoutingTests
{
    [Fact]
    public void SelectNearestReachableExitApproachTile_IgnoresUnreachableEarlierCandidates()
    {
        var fallback = new TileCoord(3, 9);
        var candidates = new[]
        {
            fallback,
            new TileCoord(2, 10),
            new TileCoord(4, 10),
        };
        var routeCosts = new Dictionary<TileCoord, int>
        {
            [new TileCoord(2, 10)] = 6,
            [new TileCoord(4, 10)] = 2,
        };

        var selected = BuildingWorkNavigator.SelectNearestReachableExitApproachTile(
            candidates,
            routeCosts,
            fallback);

        Assert.Equal(new TileCoord(4, 10), selected);
    }

    [Fact]
    public void SelectNearestReachableExitApproachTile_UsesStableCandidateOrderForEqualCosts()
    {
        var fallback = new TileCoord(3, 9);
        var firstReachable = new TileCoord(2, 10);
        var secondReachable = new TileCoord(4, 10);
        var candidates = new[] { fallback, firstReachable, secondReachable };
        var routeCosts = new Dictionary<TileCoord, int>
        {
            [firstReachable] = 4,
            [secondReachable] = 4,
        };

        var selected = BuildingWorkNavigator.SelectNearestReachableExitApproachTile(
            candidates,
            routeCosts,
            fallback);

        Assert.Equal(firstReachable, selected);
    }

    [Fact]
    public void SelectNearestReachableExitApproachTile_ReturnsFallbackWhenNoCandidateIsReachable()
    {
        var fallback = new TileCoord(3, 9);
        var candidates = new[]
        {
            fallback,
            new TileCoord(2, 10),
            new TileCoord(4, 10),
        };

        var selected = BuildingWorkNavigator.SelectNearestReachableExitApproachTile(
            candidates,
            new Dictionary<TileCoord, int>(),
            fallback);

        Assert.Equal(fallback, selected);
    }
}
