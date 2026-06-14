using Dayswork.Core.Domain;
using Dayswork.Core.Shifts;
using Xunit;

namespace Dayswork.Tests.Shifts;

public sealed class ScopeDrivenRuntimeAlignmentTests
{
    private readonly ShiftPlanBuilder _builder = new();

    [Fact]
    public void AnimalBuildingScopes_Create_AnimalBatches_Without_OutdoorZones()
    {
        var scopes = WorkScopeSet.WithSingleGreenhouse(
            null,
            new[] { new AnimalBuildingScope("Barn", AnimalBuildingTier.Barn) },
            null);

        var result = _builder.BuildBatchPlan(scopes, new[] { TaskKind.PetAnimals }.ToHashSet());

        Assert.Equal(
            new[] { BatchKind.AnimalBuilding, BatchKind.OutdoorAnimals },
            result.Select(batch => batch.Kind));
    }

    [Fact]
    public void GreenhouseCropWork_Stays_Separate_From_OutdoorCropWork()
    {
        var scopes = WorkScopeSet.WithSingleGreenhouse(
            new OutdoorWorkScope(new[] { Zone("Farm") }, 1),
            Array.Empty<AnimalBuildingScope>(),
            new GreenhouseWorkScope("Greenhouse"));

        var result = _builder.BuildBatchPlan(scopes, new[] { TaskKind.HarvestCrops }.ToHashSet());

        Assert.Equal(
            new[] { BatchKind.Greenhouse, BatchKind.OutdoorCrops },
            result.Select(batch => batch.Kind));
    }

    private static Zone Zone(string locationName, int x = 0, int y = 0) =>
        new(locationName, new TileCoord(x, y), new TileCoord(x + 1, y + 1));
}
