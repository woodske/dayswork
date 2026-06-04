namespace Dayswork.Tests.ManageCrops;

using Dayswork.Core.Crops;
using Dayswork.Core.Domain;
using Xunit;

public sealed class CropPlanSerializationTests
{
    [Fact]
    public void MapDomainToDto_EmptyPlan_ReturnsNull()
    {
        var dto = CropPlanSerialization.MapDomainToDto(CropPlan.Empty);

        Assert.Null(dto);
    }

    [Fact]
    public void MapDtoToDomain_NullPlan_ReturnsEmpty()
    {
        var plan = CropPlanSerialization.MapDtoToDomain(null);

        Assert.False(plan.IsEnabled);
    }

    [Fact]
    public void MapDomainToDto_NonEmptyPlan_RoundTrips()
    {
        var plan = CreatePlan();

        var dto = CropPlanSerialization.MapDomainToDto(plan);
        var hydrated = CropPlanSerialization.MapDtoToDomain(dto);

        Assert.True(hydrated.IsEnabled);
        Assert.Equal(plan.Assignments[0].Zone, hydrated.Assignments[0].Zone);
        Assert.Equal(plan.Assignments[0].Choices[0].Crop.SeedItemId, hydrated.Assignments[0].Choices[0].Crop.SeedItemId);
    }

    private static CropPlan CreatePlan()
    {
        var crop = new CropDescriptor("crop.parsnip", "seed.parsnip", null, 4, null, null, new[] { Season.Spring });
        var assignment = new CropZoneAssignment(
            new Zone("Farm", new TileCoord(0, 0), new TileCoord(1, 1)),
            CropAssignmentMode.Seasonal,
            new[] { new SeasonCropChoice(Season.Spring, crop, StorePreference.Pierre) });
        return new CropPlan(new[] { assignment });
    }
}
