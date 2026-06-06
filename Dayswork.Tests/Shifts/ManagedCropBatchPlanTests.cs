using Dayswork.Core.Crops;
using Dayswork.Core.Domain;
using Dayswork.Core.Shifts;
using Xunit;

namespace Dayswork.Tests.Shifts;

public sealed class ManagedCropBatchPlanTests
{
    private readonly ShiftPlanBuilder _sut = new();

    private static ManagedCropWorkScope FarmManagedScope()
    {
        var crop = new CropDescriptor("(O)24", "472", null, 4, null, null, new[] { Season.Spring });
        var choice = new SeasonCropChoice(Season.Spring, crop);
        var assignment = new CropZoneAssignment(
            new Zone("Farm", new TileCoord(0, 0), new TileCoord(2, 2)),
            CropAssignmentMode.Seasonal,
            new[] { choice });
        return new ManagedCropWorkScope(new[] { assignment });
    }

    private static WorkScopeSet Scopes(
        OutdoorWorkScope? outdoor = null,
        ManagedCropWorkScope? managed = null) =>
        new(outdoor, System.Array.Empty<AnimalBuildingScope>(), System.Array.Empty<GreenhouseWorkScope>(), managed);

    [Fact]
    public void ManagedPlanOnly_EmitsSingleManagedCropsBatchForFarm()
    {
        var result = _sut.BuildBatchPlan(Scopes(managed: FarmManagedScope()), new HashSet<TaskKind>());

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

        var result = _sut.BuildBatchPlan(Scopes(outdoor, FarmManagedScope()), enabled);

        Assert.Equal(
            new[] { BatchKind.ManagedCrops, BatchKind.OutdoorCrops, BatchKind.OutdoorClearing },
            result.Select(batch => batch.Kind));
    }

    [Fact]
    public void EmptyManagedPlan_EmitsNoManagedCropsBatch()
    {
        var result = _sut.BuildBatchPlan(Scopes(managed: new ManagedCropWorkScope(System.Array.Empty<CropZoneAssignment>())), new HashSet<TaskKind>());

        Assert.DoesNotContain(result, batch => batch.Kind == BatchKind.ManagedCrops);
    }
}
