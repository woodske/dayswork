using Dayswork.Core.Domain;
using Dayswork.Core.Shifts;
using Xunit;

namespace Dayswork.Tests.Shifts;

/// <summary>
/// Travel-aware within-category batch ordering (architecture review #3). The animal-building path
/// exercises the shared nearest-neighbor helper end-to-end: chained order, pair adjacency,
/// forage-last, missing-anchor fallback, and tie determinism. Machines / fish ponds / managed-crop /
/// greenhouse slots route through the exact same helper.
/// </summary>
public sealed class BatchOrderingTests
{
    private readonly ShiftPlanBuilder _sut = new();

    private static WorkScopeSet Scopes(IReadOnlyList<AnimalBuildingScope> animalBuildings) =>
        WorkScopeSet.WithSingleGreenhouse(null, animalBuildings, null);

    private static IReadOnlySet<TaskKind> Enabled(params TaskKind[] tasks) => tasks.ToHashSet();

    private static BatchOrderingContext Ordering(TileCoord start, params (string name, int x, int y)[] anchors)
    {
        var dict = anchors.ToDictionary(a => a.name, a => new TileCoord(a.x, a.y), StringComparer.Ordinal);
        return new BatchOrderingContext(dict, start);
    }

    private static IEnumerable<string> AnimalBuildingNames(IEnumerable<WorkBatch> batches) =>
        batches.Where(b => b.Kind == BatchKind.AnimalBuilding).Select(b => b.LocationName);

    [Fact]
    public void NearestNeighbor_ReordersBuildings_ByAnchorNotName()
    {
        var scopes = Scopes(new[]
        {
            new AnimalBuildingScope("Barn", AnimalBuildingTier.Barn),
            new AnimalBuildingScope("Coop", AnimalBuildingTier.Coop),
            new AnimalBuildingScope("Hutch", AnimalBuildingTier.BigBarn),
        });
        // From start: Coop (d1) → Hutch (d2) → Barn (d7). Name order would be Barn, Coop, Hutch.
        var ordering = Ordering(new TileCoord(0, 0), ("Coop", 1, 0), ("Hutch", 3, 0), ("Barn", 10, 0));

        var result = _sut.BuildBatchPlan(scopes, Enabled(TaskKind.FeedAnimals), TaskKindSets.DefaultCategoryPriority, ordering);

        Assert.Equal(new[] { "Coop", "Hutch", "Barn" }, AnimalBuildingNames(result));
    }

    [Fact]
    public void NearestNeighbor_KeepsBuildingPairAdjacent_AndForageLast()
    {
        var scopes = Scopes(new[]
        {
            new AnimalBuildingScope("Barn", AnimalBuildingTier.Barn),
            new AnimalBuildingScope("Coop", AnimalBuildingTier.Coop),
            new AnimalBuildingScope("Hutch", AnimalBuildingTier.BigBarn),
        });
        var ordering = Ordering(new TileCoord(0, 0), ("Coop", 1, 0), ("Hutch", 3, 0), ("Barn", 10, 0));

        var result = _sut.BuildBatchPlan(
            scopes,
            Enabled(TaskKind.FeedAnimals, TaskKind.PetAnimals, TaskKind.CollectAnimalProducts),
            TaskKindSets.DefaultCategoryPriority,
            ordering);

        // Each building's interior+grazing pair stays together, in NN order, forage truffles last.
        Assert.Equal(
            new[]
            {
                BatchKind.AnimalBuilding, BatchKind.OutdoorAnimals,
                BatchKind.AnimalBuilding, BatchKind.OutdoorAnimals,
                BatchKind.AnimalBuilding, BatchKind.OutdoorAnimals,
                BatchKind.FarmForage,
            },
            result.Select(b => b.Kind));
        Assert.Equal(
            new[] { "Coop", "Coop", "Hutch", "Hutch", "Barn", "Barn", "Farm" },
            result.Select(b => b.LocationName));
    }

    [Fact]
    public void MissingAnchor_SortsLastInNameOrder()
    {
        var scopes = Scopes(new[]
        {
            new AnimalBuildingScope("Aardvark", AnimalBuildingTier.Barn),   // no anchor
            new AnimalBuildingScope("Coop", AnimalBuildingTier.Coop),       // anchored
            new AnimalBuildingScope("Zebra", AnimalBuildingTier.BigBarn),   // anchored
        });
        var ordering = Ordering(new TileCoord(0, 0), ("Coop", 1, 0), ("Zebra", 2, 0));

        var result = _sut.BuildBatchPlan(scopes, Enabled(TaskKind.FeedAnimals), TaskKindSets.DefaultCategoryPriority, ordering);

        // Anchored ones chain first (Coop, Zebra); the unanchored one sorts last despite name order.
        Assert.Equal(new[] { "Coop", "Zebra", "Aardvark" }, AnimalBuildingNames(result));
    }

    [Fact]
    public void EquidistantAnchors_BreakTieByNameOrdinal_Deterministic()
    {
        var scopes = Scopes(new[]
        {
            new AnimalBuildingScope("Beta", AnimalBuildingTier.Barn),
            new AnimalBuildingScope("Alpha", AnimalBuildingTier.Coop),
        });
        // Both anchors are distance 5 from start → tie broken by name ordinal (Alpha before Beta).
        var ordering = Ordering(new TileCoord(0, 0), ("Beta", 5, 0), ("Alpha", 0, 5));

        var result = _sut.BuildBatchPlan(scopes, Enabled(TaskKind.FeedAnimals), TaskKindSets.DefaultCategoryPriority, ordering);

        Assert.Equal(new[] { "Alpha", "Beta" }, AnimalBuildingNames(result));
    }

    [Fact]
    public void NoOrderingContext_PreservesAlphabeticalOrder()
    {
        var scopes = Scopes(new[]
        {
            new AnimalBuildingScope("Hutch", AnimalBuildingTier.BigBarn),
            new AnimalBuildingScope("Barn", AnimalBuildingTier.Barn),
            new AnimalBuildingScope("Coop", AnimalBuildingTier.Coop),
        });

        // Null ordering (the default) ⇒ today's name ordering, unchanged.
        var result = _sut.BuildBatchPlan(scopes, Enabled(TaskKind.FeedAnimals), TaskKindSets.DefaultCategoryPriority);

        Assert.Equal(new[] { "Barn", "Coop", "Hutch" }, AnimalBuildingNames(result));
    }
}
