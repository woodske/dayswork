using Dayswork.Core.Domain;
using Dayswork.Core.Shifts;
using Xunit;

namespace Dayswork.Tests.Routing;

public sealed class WorkerRouteSelectorTests
{
    private readonly WorkerRouteSelector _selector = new();
    private readonly TaskPriorityOrderer _priority = new();

    [Fact]
    public void Nearer_reachable_candidate_wins_within_animal_care()
    {
        var farFeed = Candidate(1, TaskKind.FeedAnimals, routeCost: 8, stableOrder: 0);
        var nearProduct = Candidate(2, TaskKind.CollectAnimalProducts, routeCost: 2, stableOrder: 1);

        var selected = _selector.Select(new[] { farFeed, nearProduct });

        Assert.Equal(nearProduct, selected);
    }

    [Fact]
    public void Field_work_follows_sweep_order_over_proximity()
    {
        // Crops/Fieldwork candidates carry their sweep position as SelectionKey: the next tile in
        // the serpentine sweep wins even when another tile is closer right now.
        var nearButLaterInSweep = Candidate(1, TaskKind.WaterCrops, routeCost: 1, stableOrder: 7);
        var nextInSweep = Candidate(2, TaskKind.WaterCrops, routeCost: 5, stableOrder: 3);

        var selected = _selector.Select(new[] { nearButLaterInSweep, nextInSweep });

        Assert.Equal(nextInSweep, selected);
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
    public void Equal_sweep_position_and_priority_falls_back_to_route_cost()
    {
        var far = Candidate(1, TaskKind.ClearWeeds, routeCost: 6, stableOrder: 2, selectionKey: 4);
        var near = Candidate(2, TaskKind.ClearWeeds, routeCost: 3, stableOrder: 5, selectionKey: 4);

        var selected = _selector.Select(new[] { far, near });

        Assert.Equal(near, selected);
    }

    [Fact]
    public void No_reachable_candidates_returns_no_selection()
    {
        var blocked = Candidate(1, TaskKind.ClearWeeds, routeCost: 0, stableOrder: 0, reachable: false);

        var selected = _selector.Select(new[] { blocked });

        Assert.Null(selected);
    }

    // Mirrors the production wiring in TryEvaluateCandidateRoute: AnimalCare candidates are
    // nearest-first (SelectionKey = route cost), everything else follows the precomputed sweep
    // (SelectionKey = StableOrder = queue position).
    private WorkerRouteCandidate Candidate(
        int id,
        TaskKind task,
        int routeCost,
        int stableOrder,
        bool reachable = true,
        int? selectionKey = null) =>
        new(
            id,
            task,
            _priority.Rank(task),
            stableOrder,
            selectionKey ?? (TaskKindSets.CategoryOf(task) == TaskCategory.AnimalCare ? routeCost : stableOrder),
            new TileCoord(id, stableOrder),
            reachable,
            routeCost);
}
