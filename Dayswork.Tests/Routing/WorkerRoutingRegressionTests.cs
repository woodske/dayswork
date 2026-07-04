using Dayswork.Core.Domain;
using Dayswork.Core.Shifts;
using Dayswork.Orchestration;
using Xunit;

namespace Dayswork.Tests.Routing;

public sealed class WorkerRoutingRegressionTests
{
    private readonly WorkerRouteSelector _selector = new();
    private readonly TaskPriorityOrderer _priority = new();

    [Fact]
    public void Field_work_sweep_position_wins_over_standing_proximity()
    {
        // Serpentine routing: fieldwork follows the precomputed sweep (SelectionKey = queue
        // position), so the next tile in the sweep wins even when the worker is already standing
        // next to a later one. (Pre-sweep behavior: routeCost 0 won outright.)
        var currentSide = Candidate(1, TaskKind.ClearWeeds, routeCost: 0, stableOrder: 1);
        var nextInSweep = Candidate(2, TaskKind.ClearWeeds, routeCost: 6, stableOrder: 0);

        var selected = _selector.Select(new[] { nextInSweep, currentSide });

        Assert.Equal(nextInSweep, selected);
    }

    [Fact]
    public void Animal_care_keeps_zero_route_cost_current_tile_preference()
    {
        // AnimalCare (moving animals + troughs/floor forage at the same rank) stays nearest-first.
        var currentSide = Candidate(1, TaskKind.CollectAnimalProducts, routeCost: 0, stableOrder: 1);
        var farther = Candidate(2, TaskKind.CollectAnimalProducts, routeCost: 6, stableOrder: 0);

        var selected = _selector.Select(new[] { farther, currentSide });

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

    [Fact]
    public void Shopping_return_prefers_tile_action_warp_when_route_cost_and_target_tie()
    {
        var mapProperty = ShoppingWarp(
            "map",
            routeCost: 4,
            targetRank: 1,
            ManagedShoppingCoordinator.WarpEdgeSourceKind.MapProperty,
            new TileCoord(9, 22));
        var tileAction = ShoppingWarp(
            "action",
            routeCost: 4,
            targetRank: 1,
            ManagedShoppingCoordinator.WarpEdgeSourceKind.TileAction,
            new TileCoord(9, 22));

        var selected = OrderShoppingWarps(mapProperty, tileAction).First();

        Assert.Equal(tileAction, selected);
    }

    [Fact]
    public void Shopping_return_keeps_route_cost_ahead_of_tile_action_priority()
    {
        var nearerMapProperty = ShoppingWarp(
            "near-map",
            routeCost: 2,
            targetRank: 1,
            ManagedShoppingCoordinator.WarpEdgeSourceKind.MapProperty,
            new TileCoord(9, 22));
        var fartherTileAction = ShoppingWarp(
            "far-action",
            routeCost: 8,
            targetRank: 1,
            ManagedShoppingCoordinator.WarpEdgeSourceKind.TileAction,
            new TileCoord(9, 22));

        var selected = OrderShoppingWarps(fartherTileAction, nearerMapProperty).First();

        Assert.Equal(nearerMapProperty, selected);
    }

    [Fact]
    public void Shopping_return_keeps_target_rank_ahead_of_tile_action_priority()
    {
        var preferredTargetMapProperty = ShoppingWarp(
            "preferred-target",
            routeCost: 4,
            targetRank: 0,
            ManagedShoppingCoordinator.WarpEdgeSourceKind.MapProperty,
            new TileCoord(9, 22));
        var lowerPriorityTargetAction = ShoppingWarp(
            "lower-target-action",
            routeCost: 4,
            targetRank: 1,
            ManagedShoppingCoordinator.WarpEdgeSourceKind.TileAction,
            new TileCoord(9, 22));

        var selected = OrderShoppingWarps(lowerPriorityTargetAction, preferredTargetMapProperty).First();

        Assert.Equal(preferredTargetMapProperty, selected);
    }

    // Mirrors the production wiring in TryEvaluateCandidateRoute: AnimalCare candidates are
    // nearest-first (SelectionKey = route cost), everything else follows the precomputed sweep
    // (SelectionKey = StableOrder = queue position).
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
            TaskKindSets.CategoryOf(task) == TaskCategory.AnimalCare ? routeCost : stableOrder,
            new TileCoord(id, stableOrder),
            reachable,
            routeCost);

    private static ShoppingWarpProbe ShoppingWarp(
        string id,
        int routeCost,
        int targetRank,
        ManagedShoppingCoordinator.WarpEdgeSourceKind sourceKind,
        TileCoord approachTile) =>
        new(id, routeCost, targetRank, sourceKind, approachTile);

    private static IReadOnlyList<ShoppingWarpProbe> OrderShoppingWarps(params ShoppingWarpProbe[] warps) =>
        ManagedShoppingCoordinator.OrderWarpEdgesByRoutePriority(
                warps,
                warp => warp.RouteCost,
                warp => warp.TargetRank,
                warp => warp.SourceKind,
                warp => warp.ApproachTile)
            .ToList();

    private sealed record ShoppingWarpProbe(
        string Id,
        int RouteCost,
        int TargetRank,
        ManagedShoppingCoordinator.WarpEdgeSourceKind SourceKind,
        TileCoord ApproachTile);
}
