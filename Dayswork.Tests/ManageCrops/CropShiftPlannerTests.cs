namespace Dayswork.Tests.ManageCrops;

using Dayswork.Core.Crops;
using Dayswork.Core.Domain;
using Xunit;

public sealed class CropShiftPlannerTests
{
    [Fact]
    public void Plan_OrdersTileActionsByDependency()
    {
        var planner = new CropShiftPlanner();
        var crop = new CropDescriptor("crop.parsnip", "seed.parsnip", "fert.basic", 4, 3, null, new[] { Season.Spring });
        var assignment = new CropZoneAssignment(
            new Zone("Farm", new TileCoord(0, 0), new TileCoord(0, 0)),
            CropAssignmentMode.Seasonal,
            new[] { new SeasonCropChoice(Season.Spring, crop, StorePreference.InputChestOnly) });
        var field = new FieldState(
            "Farm",
            new GameDate(1, Season.Spring, 1),
            false,
            new[] { new TileState(new TileCoord(0, 0), false, false, false, false, false, false) });
        var inventory = new SupplyInventory(new Dictionary<string, int>
        {
            ["seed.parsnip"] = 1,
            ["fert.basic"] = 1,
        });

        var result = planner.Plan(assignment, field, inventory, Array.Empty<ShopStockSnapshot>(), false);

        Assert.Equal(
            new[]
            {
                ManagedCropActionKind.Till,
                ManagedCropActionKind.Fertilize,
                ManagedCropActionKind.PlantSeed,
                ManagedCropActionKind.Water,
            },
            result.AllActions.Select(action => action.Kind));
    }

    [Fact]
    public void Plan_FertilizerRequiredWithOnlySeed_DoesNotPlantSeed()
    {
        var planner = new CropShiftPlanner();
        var crop = new CropDescriptor("crop.parsnip", "seed.parsnip", "fert.basic", 4, 3, null, new[] { Season.Spring });
        var assignment = new CropZoneAssignment(
            new Zone("Farm", new TileCoord(0, 0), new TileCoord(0, 0)),
            CropAssignmentMode.Seasonal,
            new[] { new SeasonCropChoice(Season.Spring, crop, StorePreference.InputChestOnly) });
        var field = new FieldState(
            "Farm",
            new GameDate(1, Season.Spring, 1),
            false,
            new[] { new TileState(new TileCoord(0, 0), false, false, false, true, false, false) });
        var inventory = new SupplyInventory(new Dictionary<string, int> { ["seed.parsnip"] = 1 });

        var result = planner.Plan(assignment, field, inventory, Array.Empty<ShopStockSnapshot>(), false);

        Assert.DoesNotContain(result.AllActions, action => action.Kind == ManagedCropActionKind.PlantSeed);
    }
}
