using Dayswork.Core.Domain;
using Dayswork.Core.Shifts;
using Xunit;

namespace Dayswork.Tests.Routing;

public sealed class WorkerRouteSelectorTests
{
    private readonly WorkerRouteSelector _selector = new();
    private readonly TaskPriorityOrderer _priority = new();

    [Fact]
    public void Nearer_reachable_candidate_wins_over_farther_higher_priority_candidate()
    {
        var farFeed = Candidate(1, TaskKind.FeedAnimals, routeCost: 8, stableOrder: 0);
        var nearProduct = Candidate(2, TaskKind.CollectAnimalProducts, routeCost: 2, stableOrder: 1);

        var selected = _selector.Select(new[] { farFeed, nearProduct });

        Assert.Equal(nearProduct, selected);
    }

    [Fact]
    public void Equal_route_cost_uses_category_priority()
    {
        // FeedAnimals is in AnimalCare (default rank 0); WaterCrops is in Crops (rank 1).
        // With equal route cost, the higher-priority category wins.
        var crops = Candidate(1, TaskKind.WaterCrops, routeCost: 4, stableOrder: 0);
        var animal = Candidate(2, TaskKind.FeedAnimals, routeCost: 4, stableOrder: 1);

        var selected = _selector.Select(new[] { crops, animal });

        Assert.Equal(animal, selected);
    }

    [Fact]
    public void Higher_priority_category_wins_over_nearer_lower_priority()
    {
        // Category priority is strict: a far AnimalCare task beats a near Fieldwork task.
        var nearFieldwork = Candidate(1, TaskKind.ClearWeeds, routeCost: 1, stableOrder: 0);
        var farAnimal = Candidate(2, TaskKind.FeedAnimals, routeCost: 9, stableOrder: 1);

        var selected = _selector.Select(new[] { nearFieldwork, farAnimal });

        Assert.Equal(farAnimal, selected);
    }

    [Fact]
    public void Equal_route_cost_and_priority_uses_stable_order()
    {
        var later = Candidate(1, TaskKind.ClearWeeds, routeCost: 3, stableOrder: 10);
        var earlier = Candidate(2, TaskKind.ClearWeeds, routeCost: 3, stableOrder: 2);

        var selected = _selector.Select(new[] { later, earlier });

        Assert.Equal(earlier, selected);
    }

    [Fact]
    public void No_reachable_candidates_returns_no_selection()
    {
        var blocked = Candidate(1, TaskKind.ClearWeeds, routeCost: 0, stableOrder: 0, reachable: false);

        var selected = _selector.Select(new[] { blocked });

        Assert.Null(selected);
    }

    private WorkerRouteCandidate Candidate(
        int id,
        TaskKind task,
        int routeCost,
        int stableOrder,
        bool reachable = true) =>
        new(
            id,
            task,
            _priority.Rank(task),
            stableOrder,
            new TileCoord(id, stableOrder),
            reachable,
            routeCost);
}
