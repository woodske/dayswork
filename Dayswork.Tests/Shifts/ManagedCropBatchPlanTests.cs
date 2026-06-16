using Dayswork.Core.Crops;
using Dayswork.Core.Domain;
using Dayswork.Core.Shifts;
using Xunit;

namespace Dayswork.Tests.Shifts;

public sealed class ManagedCropBatchPlanTests
{
    private readonly ShiftPlanBuilder _sut = new();

    private static ManagedCropWorkScope ManagedScope(params string[] locations)
    {
        var crop = new CropDescriptor("(O)24", "472", null, 4, null, null, new[] { Season.Spring });
        var choice = new SeasonCropChoice(Season.Spring, crop);
        var assignments = locations.Select((location, index) => new CropZoneAssignment(
            new Zone(location, new TileCoord(index, index), new TileCoord(index + 2, index + 2)),
            string.Equals(location, "Farm", StringComparison.Ordinal)
                ? CropAssignmentMode.Seasonal
                : CropAssignmentMode.SeasonAgnostic,
            new[] { choice }));
        return new ManagedCropWorkScope(assignments.ToList());
    }

    private static ManagedCropWorkScope FarmManagedScope() => ManagedScope("Farm");

    private static WorkScopeSet Scopes(
        OutdoorWorkScope? outdoor = null,
        ManagedCropWorkScope? managed = null,
        IReadOnlyList<GreenhouseWorkScope>? greenhouses = null) =>
        new(outdoor, System.Array.Empty<AnimalBuildingScope>(), greenhouses ?? System.Array.Empty<GreenhouseWorkScope>(), managed);

    [Fact]
    public void ManagedPlanOnly_EmitsSingleManagedCropsBatchForFarm()
    {
        var result = _sut.BuildBatchPlan(Scopes(managed: FarmManagedScope()), new HashSet<TaskKind>(), TaskKindSets.DefaultCategoryPriority);

        var batch = Assert.Single(result);
        Assert.Equal(BatchKind.ManagedCrops, batch.Kind);
        Assert.Equal("Farm", batch.LocationName);
        Assert.Empty(batch.TileWork);
    }

    [Fact]
    public void ManagedBatch_OrderedBeforeGeneralOutdoorCropAndClearingBatches()
    {
        var outdoor = new OutdoorWorkScope(new[] { new Zone("Farm", new TileCoord(10, 10), new TileCoord(12, 12)) }, 1);
        var enabled = new HashSet<TaskKind> { TaskKind.HarvestCrops, TaskKind.ClearWeeds };

        var result = _sut.BuildBatchPlan(Scopes(outdoor, FarmManagedScope()), enabled, TaskKindSets.DefaultCategoryPriority);

        Assert.Equal(
            new[] { BatchKind.ManagedCrops, BatchKind.OutdoorCrops, BatchKind.OutdoorClearing },
            result.Select(batch => batch.Kind));
    }

    [Fact]
    public void EmptyManagedPlan_EmitsNoManagedCropsBatch()
    {
        var result = _sut.BuildBatchPlan(Scopes(managed: new ManagedCropWorkScope(System.Array.Empty<CropZoneAssignment>())), new HashSet<TaskKind>(), TaskKindSets.DefaultCategoryPriority);

        Assert.DoesNotContain(result, batch => batch.Kind == BatchKind.ManagedCrops);
    }

    [Fact]
    public void ManagedPlan_EmitsOneManagedBatchPerDistinctLocation()
    {
        var managed = ManagedScope("Farm", "Greenhouse", "Custom_GrandpasShedGreenhouse", "Greenhouse");

        var result = _sut.BuildBatchPlan(Scopes(managed: managed), new HashSet<TaskKind>(), TaskKindSets.DefaultCategoryPriority);

        Assert.Equal(
            new[] { "Custom_GrandpasShedGreenhouse", "Greenhouse", "Farm" },
            result.Where(batch => batch.Kind == BatchKind.ManagedCrops).Select(batch => batch.LocationName));
    }

    [Fact]
    public void ManagedGreenhouseBatch_IsOrderedBeforeGeneralGreenhouseBatch()
    {
        var managed = ManagedScope("Greenhouse");
        var greenhouses = new[] { new GreenhouseWorkScope("Greenhouse") };
        var enabled = new HashSet<TaskKind> { TaskKind.HarvestCrops };

        var result = _sut.BuildBatchPlan(Scopes(managed: managed, greenhouses: greenhouses), enabled, TaskKindSets.DefaultCategoryPriority);

        Assert.Equal(
            new[] { BatchKind.ManagedCrops, BatchKind.Greenhouse },
            result.Select(batch => batch.Kind));
        Assert.All(result, batch => Assert.Equal("Greenhouse", batch.LocationName));
    }
}
