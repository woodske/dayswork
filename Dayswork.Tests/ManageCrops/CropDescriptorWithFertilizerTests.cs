namespace Dayswork.Tests.ManageCrops;

using Dayswork.Core.Crops;
using Dayswork.Core.Domain;
using Xunit;

public sealed class CropDescriptorWithFertilizerTests
{
    private static CropDescriptor Base() =>
        new("crop.corn", "seed.corn", null, 14, 11, 4, new[] { Season.Summer, Season.Fall });

    [Fact]
    public void WithFertilizer_SetsFertilizerAndPreservesGrowthFields()
    {
        var result = Base().WithFertilizer("fert.basic");

        Assert.Equal("fert.basic", result.FertilizerItemId);
        Assert.True(result.RequiresFertilizer);
        Assert.Equal("crop.corn", result.CropItemId);
        Assert.Equal("seed.corn", result.SeedItemId);
        Assert.Equal(14, result.DaysToFirstHarvest);
        Assert.Equal(11, result.FertilizedDaysToFirstHarvest);
        Assert.Equal(4, result.RegrowDays);
        Assert.Equal(new[] { Season.Summer, Season.Fall }, result.Seasons);
    }

    [Fact]
    public void WithFertilizer_NullClearsFertilizer()
    {
        var withFertilizer = Base().WithFertilizer("fert.basic");

        var cleared = withFertilizer.WithFertilizer(null);

        Assert.Null(cleared.FertilizerItemId);
        Assert.False(cleared.RequiresFertilizer);
    }
}
