namespace Dayswork.Tests.ManageCrops;

using Dayswork.Core.Crops;
using Xunit;

public sealed class CropSupplyPlannerTests
{
    [Fact]
    public void CompletableTiles_FertilizerRequired_IsCappedByBothComponents()
    {
        var planner = new CropSupplyPlanner();
        var crop = new CropDescriptor("crop.test", "seed.test", "fert.basic", 4, 3, null, Array.Empty<Dayswork.Core.Domain.Season>());
        var inventory = new SupplyInventory(new Dictionary<string, int>
        {
            ["seed.test"] = 5,
            ["fert.basic"] = 2,
        });

        Assert.Equal(2, planner.CompletableTiles(crop, 10, inventory));
    }

    [Fact]
    public void CalculatePurchaseTargets_ChestSupplyOnly_ReturnsNoTargets()
    {
        var planner = new CropSupplyPlanner();
        var crop = new CropDescriptor("crop.test", "seed.test", null, 4, null, null, Array.Empty<Dayswork.Core.Domain.Season>());

        var targets = planner.CalculatePurchaseTargets(crop, 4, SupplyInventory.Empty, StorePreference.InputChestOnly);

        Assert.Empty(targets);
    }

    [Fact]
    public void CalculatePurchaseTargets_MissingFertilizerEverywhere_ReturnsNoSeedOnlyTarget()
    {
        var planner = new CropSupplyPlanner();
        var crop = new CropDescriptor("crop.test", "seed.test", "fert.basic", 4, 3, null, Array.Empty<Dayswork.Core.Domain.Season>());

        var targets = planner.CalculatePurchaseTargets(crop, 4, SupplyInventory.Empty, StorePreference.Either);

        Assert.Empty(targets);
    }

    [Fact]
    public void CalculatePurchaseTargets_FertilizerUnlimitedInStore_DoesNotOverflowAndReturnsSeedTarget()
    {
        // Regression: int.MaxValue store stock + any chest quantity overflowed to negative, causing
        // the early-return guard to fire and return empty targets for zones with unlimited fertilizer.
        var planner = new CropSupplyPlanner();
        var crop = new CropDescriptor("crop.melon", "479", "465", 8, 6, null, Array.Empty<Dayswork.Core.Domain.Season>());
        var inventory = new SupplyInventory(new Dictionary<string, int> { ["465"] = 6 });
        var stock = new List<ShopStockSnapshot>
        {
            new(Store.Pierre, isOpen: true, new Dictionary<string, int> { ["465"] = int.MaxValue }),
        };

        var targets = planner.CalculatePurchaseTargets(crop, 6, inventory, StorePreference.Pierre, stock);

        Assert.Single(targets);
        Assert.Equal("479", targets[0].ItemId);
        Assert.Equal(6, targets[0].Quantity);
    }
}
