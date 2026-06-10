using Dayswork.Core.Domain;
using Dayswork.Core.Shifts;
using Xunit;

namespace Dayswork.Tests.Routing;

public sealed class WorkerRoutingRegressionTests
{
    private readonly WorkerRouteSelector _selector = new();
    private readonly TaskPriorityOrderer _priority = new();

    [Fact]
    public void Standing_on_valid_interaction_tile_wins_with_zero_route_cost()
    {
        var currentSide = Candidate(1, TaskKind.ClearWeeds, routeCost: 0, stableOrder: 1);
        var preferredTopSide = Candidate(2, TaskKind.ClearWeeds, routeCost: 6, stableOrder: 0);

        var selected = _selector.Select(new[] { preferredTopSide, currentSide });

        Assert.Equal(currentSide, selected);
    }

    [Fact]
    public void Egg_collection_uses_reachable_side_when_preferred_side_is_blocked()
    {
        var blockedTop = Candidate(1, TaskKind.CollectAnimalProducts, routeCost: 0, stableOrder: 0, reachable: false);
        var reachableSide = Candidate(2, TaskKind.CollectAnimalProducts, routeCost: 3, stableOrder: 1);

        var selected = _selector.Select(new[] { blockedTop, reachableSide });

        Assert.Equal(reachableSide, selected);
    }

    [Fact]
    public void Nearer_animal_work_wins_inside_active_animal_batch()
    {
        var farAnimal = Candidate(1, TaskKind.PetAnimals, routeCost: 14, stableOrder: 0);
        var nearAnimal = Candidate(2, TaskKind.PetAnimals, routeCost: 4, stableOrder: 1);

        var selected = _selector.Select(new[] { farAnimal, nearAnimal });

        Assert.Equal(nearAnimal, selected);
    }

    [Fact]
    public void Hopper_work_can_be_retried_after_enabled_product_collection_clears_route()
    {
        var blockedHopper = Candidate(1, TaskKind.FeedAnimals, routeCost: 0, stableOrder: 0, reachable: false);
        var blockingEgg = Candidate(2, TaskKind.CollectAnimalProducts, routeCost: 1, stableOrder: 1);

        var firstSelection = _selector.Select(new[] { blockedHopper, blockingEgg });

        var clearedHopper = blockedHopper with { Reachable = true, RouteCost = 2 };
        var secondSelection = _selector.Select(new[] { clearedHopper });

        Assert.Equal(blockingEgg, firstSelection);
        Assert.Equal(clearedHopper, secondSelection);
    }

    [Fact]
    public void Disabled_product_collection_leaves_blocked_feed_unselected()
    {
        var blockedHopper = Candidate(1, TaskKind.FeedAnimals, routeCost: 0, stableOrder: 0, reachable: false);

        var selected = _selector.Select(new[] { blockedHopper });

        Assert.Null(selected);
    }

    [Fact]
    public void No_reachable_work_represents_no_progress_pass_termination()
    {
        var blockedEgg = Candidate(1, TaskKind.CollectAnimalProducts, routeCost: 0, stableOrder: 0, reachable: false);
        var blockedHopper = Candidate(2, TaskKind.FeedAnimals, routeCost: 0, stableOrder: 1, reachable: false);

        var selected = _selector.Select(new[] { blockedEgg, blockedHopper });

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
