namespace Dayswork.Tests.FishPonds;

using Dayswork.Core.Domain;
using Dayswork.Core.FishPonds;
using Dayswork.Core.Shifts;
using Xunit;

public sealed class FishPondScopeTests
{
    private readonly ShiftPlanBuilder _sut = new();

    private static WorkScopeSet ScopesWithPonds(FishPondWorkScope ponds) =>
        WorkScopeSet.WithSingleGreenhouse(
            outdoorWork: null,
            animalBuildings: System.Array.Empty<AnimalBuildingScope>(),
            greenhouseWork: null,
            managedCrops: null,
            machines: null,
            fishPonds: ponds);

    [Fact]
    public void Scope_DropsBlankLocations_Dedupes_AndSorts()
    {
        var scope = new FishPondWorkScope(new[]
        {
            new FishPondRef("Farm", new TileCoord(15, 12)),
            new FishPondRef("Farm", new TileCoord(10, 12)),
            new FishPondRef("Farm", new TileCoord(10, 12)), // duplicate
            new FishPondRef("", new TileCoord(1, 1)),        // blank location dropped
        });

        Assert.Equal(2, scope.Ponds.Count);
        Assert.Equal(new TileCoord(10, 12), scope.Ponds[0].Tile);
        Assert.Equal(new TileCoord(15, 12), scope.Ponds[1].Tile);
    }

    [Fact]
    public void EmptyScope_IsNotEnabled()
    {
        Assert.False(FishPondWorkScope.Empty.IsEnabled);
        Assert.False(new FishPondWorkScope(System.Array.Empty<FishPondRef>()).IsEnabled);
    }

    [Fact]
    public void OutputRouter_MapsProvenance_ToChosenDestination()
    {
        var chest = new ChestDestination(new ChestRef("Farm", new TileCoord(2, 2)));
        var scope = new FishPondWorkScope(new[] { new FishPondRef("Farm", new TileCoord(8, 8)) }, chest);

        var map = FishPondOutputRouter.BuildDestinationMap(scope);

        Assert.Single(map);
        Assert.Equal(chest, map[FishPondOutputRouter.Provenance]);
    }

    [Fact]
    public void OutputRouter_AutomaticDestination_MapsProvenanceToAutomatic_NotEmpty()
    {
        // Always include the provenance (even Automatic) so pond output can't fall through to an
        // unrelated per-task destination assignment.
        var automatic = new FishPondWorkScope(new[] { new FishPondRef("Farm", new TileCoord(8, 8)) });

        var map = FishPondOutputRouter.BuildDestinationMap(automatic);

        Assert.Single(map);
        Assert.IsType<AutomaticOutputDestination>(map[FishPondOutputRouter.Provenance]);
    }

    [Fact]
    public void OutputRouter_DisabledScope_IsEmpty()
    {
        Assert.Empty(FishPondOutputRouter.BuildDestinationMap(FishPondWorkScope.Empty));
        Assert.Empty(FishPondOutputRouter.BuildDestinationMap(null));
    }

    [Fact]
    public void PondsSpanningTwoLocations_EmitsOneBatchPerLocation()
    {
        var scope = new FishPondWorkScope(new[]
        {
            new FishPondRef("Farm", new TileCoord(10, 10)),
            new FishPondRef("Farm", new TileCoord(12, 12)),
            new FishPondRef("CustomFarmExpansion", new TileCoord(3, 3)),
        });

        var result = _sut.BuildBatchPlan(ScopesWithPonds(scope), new HashSet<TaskKind>(), TaskKindSets.DefaultCategoryPriority);

        Assert.Equal(2, result.Count);
        Assert.All(result, batch => Assert.Equal(BatchKind.FishPonds, batch.Kind));
        Assert.All(result, batch => Assert.Empty(batch.Tasks));
        Assert.Equal(new[] { "CustomFarmExpansion", "Farm" }, result.Select(batch => batch.LocationName));
    }

    [Fact]
    public void NoPondScope_EmitsNoFishPondBatch()
    {
        var result = _sut.BuildBatchPlan(
            ScopesWithPonds(FishPondWorkScope.Empty),
            new HashSet<TaskKind>(),
            TaskKindSets.DefaultCategoryPriority);

        Assert.Empty(result);
    }
}
