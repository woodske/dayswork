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
    public void CompletableTiles_PreFertilizedTiles_OnlyNeedSeeds()
    {
        var planner = new CropSupplyPlanner();
        var crop = new CropDescriptor("crop.test", "seed.test", "fert.basic", 4, 3, null, Array.Empty<Dayswork.Core.Domain.Season>());
        // 0 fertilizer in chest; 4 tiles already fertilized on the ground
        var inventory = new SupplyInventory(new Dictionary<string, int> { ["seed.test"] = 4 });

        Assert.Equal(4, planner.CompletableTiles(crop, 10, inventory, preFertilizedTileCount: 4));
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

    // Regression: tilled+fertilized tiles after harvest were counted as needing new fertilizer,
    // causing over-purchasing and the maxSeedPurchasesWithFertilizer cap to under-buy seeds.
    [Fact]
    public void CalculatePurchaseTargets_AllTilesPreFertilized_BuysNoFertilizer()
    {
        var planner = new CropSupplyPlanner();
        var crop = new CropDescriptor("crop.melon", "254", "465", 8, 6, null, Array.Empty<Dayswork.Core.Domain.Season>());
        var stock = new List<ShopStockSnapshot>
        {
            new(Store.Pierre, isOpen: true, new Dictionary<string, int> { ["465"] = 26, ["254"] = 100 }),
        };

        // 18 tiles, 5 seeds already in chest, 0 fertilizer in chest, but all 18 tiles are pre-fertilized
        var inventory = new SupplyInventory(new Dictionary<string, int> { ["254"] = 5 });
        var targets = planner.CalculatePurchaseTargets(crop, 18, inventory, StorePreference.Pierre, stock, preFertilizedTileCount: 18);

        Assert.DoesNotContain(targets, t => t.ItemId == "465"); // no fertilizer purchase
        var seedTarget = Assert.Single(targets, t => t.ItemId == "254");
        Assert.Equal(13, seedTarget.Quantity); // 18 tiles - 5 in chest
    }

    [Fact]
    public void CalculatePurchaseTargets_MixedFertilization_BuysOnlyUnfertilizedCount()
    {
        var planner = new CropSupplyPlanner();
        var crop = new CropDescriptor("crop.test", "seed.test", "fert.basic", 4, 3, null, Array.Empty<Dayswork.Core.Domain.Season>());
        var stock = new List<ShopStockSnapshot>
        {
            new(Store.Pierre, isOpen: true, new Dictionary<string, int> { ["fert.basic"] = 50, ["seed.test"] = 50 }),
        };

        // 16 tiles, 10 already fertilized, 6 need fertilizer
        var targets = planner.CalculatePurchaseTargets(crop, 16, SupplyInventory.Empty, StorePreference.Pierre, stock, preFertilizedTileCount: 10);

        var fertTarget = Assert.Single(targets, t => t.ItemId == "fert.basic");
        Assert.Equal(6, fertTarget.Quantity);
    }

    [Fact]
    public void CalculatePurchaseTargets_AllPreFertilized_NoFertilizerInStore_StillBuysSeeds()
    {
        var planner = new CropSupplyPlanner();
        var crop = new CropDescriptor("crop.test", "seed.test", "fert.basic", 4, 3, null, Array.Empty<Dayswork.Core.Domain.Season>());
        var stock = new List<ShopStockSnapshot>
        {
            new(Store.Pierre, isOpen: true, new Dictionary<string, int> { ["seed.test"] = 50 }), // no fertilizer in store
        };

        // Bail-out guard must not fire when all tiles are pre-fertilized (no fertilizer needed)
        var targets = planner.CalculatePurchaseTargets(crop, 8, SupplyInventory.Empty, StorePreference.Pierre, stock, preFertilizedTileCount: 8);

        var seedTarget = Assert.Single(targets, t => t.ItemId == "seed.test");
        Assert.Equal(8, seedTarget.Quantity);
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
