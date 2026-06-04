namespace Dayswork.Tests.ManageCrops;

using Dayswork.Core.Crops;
using Dayswork.Core.Domain;
using Xunit;

public sealed class PlantingViabilityCalculatorTests
{
    [Fact]
    public void IsPlantingViable_OutdoorCropTooLate_ReturnsFalse()
    {
        var calculator = new PlantingViabilityCalculator();
        var crop = new CropDescriptor("crop.parsnip", "seed.parsnip", null, 4, null, null, new[] { Season.Spring });
        var field = new FieldState("Farm", new GameDate(26, Season.Spring, 1), false, Array.Empty<TileState>());

        Assert.False(calculator.IsPlantingViable(field, crop, false));
    }

    [Fact]
    public void IsPlantingViable_FertilizedCropUsesFasterGrowth()
    {
        var calculator = new PlantingViabilityCalculator();
        var crop = new CropDescriptor("crop.test", "seed.test", "fert.basic", 4, 2, null, new[] { Season.Spring });
        var field = new FieldState("Farm", new GameDate(26, Season.Spring, 1), false, Array.Empty<TileState>());

        Assert.True(calculator.IsPlantingViable(field, crop, true));
    }

    [Fact]
    public void IsPlantingViable_GreenhouseBypassesSeasonEnd()
    {
        var calculator = new PlantingViabilityCalculator();
        var crop = new CropDescriptor("crop.ancient", "seed.ancient", null, 28, null, 7, new[] { Season.Spring });
        var field = new FieldState("Greenhouse", new GameDate(28, Season.Winter, 3), true, Array.Empty<TileState>());

        Assert.True(calculator.IsPlantingViable(field, crop, false));
    }
}
