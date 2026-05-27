using Dayswork.Core.Domain;
using Dayswork.Core.Shifts;
using Xunit;

namespace Dayswork.Tests.UWR;

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
    public void Equal_route_cost_uses_task_priority()
    {
        var product = Candidate(1, TaskKind.CollectAnimalProducts, routeCost: 4, stableOrder: 0);
        var pet = Candidate(2, TaskKind.PetAnimals, routeCost: 4, stableOrder: 1);

        var selected = _selector.Select(new[] { product, pet });

        Assert.Equal(pet, selected);
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
