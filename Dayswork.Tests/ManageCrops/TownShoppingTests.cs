namespace Dayswork.Tests.ManageCrops;

using Dayswork.Core.Crops;
using Dayswork.Core.Domain;
using Xunit;

public sealed class TownShoppingTests
{
    [Fact]
    public void ShopStockSnapshot_filters_and_exposes_prices()
    {
        var snapshot = new ShopStockSnapshot(
            Store.Pierre,
            isOpen: true,
            new Dictionary<string, int> { ["seed.parsnip"] = 12, ["missing"] = 0 },
            new Dictionary<string, int> { ["seed.parsnip"] = 20, ["free"] = 0 });

        Assert.Equal(12, snapshot.QuantityOf("seed.parsnip"));
        Assert.Equal(0, snapshot.QuantityOf("missing"));
        Assert.Equal(20, snapshot.UnitPriceOf("seed.parsnip"));
        Assert.Equal(0, snapshot.UnitPriceOf("free"));
    }

    [Fact]
    public void ShiftSupplyAggregator_groups_purchase_by_preferred_store_and_subtracts_chest_supply()
    {
        var crop = new CropDescriptor(
            "crop.parsnip",
            "seed.parsnip",
            fertilizerItemId: null,
            daysToFirstHarvest: 4,
            fertilizedDaysToFirstHarvest: null,
            regrowDays: null,
            new[] { Season.Spring });
        var assignment = new CropZoneAssignment(
            new Zone("Farm", new TileCoord(0, 0), new TileCoord(1, 0)),
            CropAssignmentMode.Seasonal,
            new[] { new SeasonCropChoice(Season.Spring, crop, StorePreference.Pierre) });
        var field = new FieldState(
            "Farm",
            new GameDate(1, Season.Spring, 1),
            isSeasonAgnosticLocation: false,
            new[]
            {
                new TileState(new TileCoord(0, 0), false, false, false, false, false, false),
                new TileState(new TileCoord(1, 0), false, false, false, false, false, false),
            });
        var stock = new[]
        {
            new ShopStockSnapshot(Store.Pierre, true, new Dictionary<string, int> { ["seed.parsnip"] = 99 }, new Dictionary<string, int> { ["seed.parsnip"] = 20 }),
            new ShopStockSnapshot(Store.Joja, true, new Dictionary<string, int> { ["seed.parsnip"] = 99 }, new Dictionary<string, int> { ["seed.parsnip"] = 25 }),
        };

        var manifest = new ShiftSupplyAggregator().BuildManifest(
            new CropPlan(new[] { assignment }),
            field,
            new SupplyInventory(new Dictionary<string, int> { ["seed.parsnip"] = 1 }),
            StorePreference.Pierre,
            stock,
            isFestivalDay: false);

        var group = Assert.Single(manifest.Groups);
        Assert.Equal(Store.Pierre, group.Store);
        var line = Assert.Single(group.Lines);
        Assert.Equal("seed.parsnip", line.ItemId);
        Assert.Equal(1, line.Quantity);
        Assert.Equal(20, line.UnitCost);
    }

    [Theory]
    [InlineData(100, 2, false)]
    [InlineData(60, 1, true)]
    [InlineData(0, 0, true)]
    public void Affordability_examples_clamp_expected_pairs(int wallet, int expectedPairs, bool shortfall)
    {
        var manifest = new ShiftPurchaseManifest(
            new[]
            {
                new StorePurchaseGroup(
                    Store.Pierre,
                    new[]
                    {
                        new ManifestLine("fert.basic", 2, 20, IsFertilizer: true),
                        new ManifestLine("seed.parsnip", 2, 30, IsFertilizer: false),
                    }),
            },
            Array.Empty<string>());

        var plan = new PurchaseAffordabilityCalculator().ClampToWallet(manifest, wallet);
        var lines = plan.Groups.SelectMany(group => group.Lines).ToList();

        Assert.Equal(expectedPairs, lines.Where(line => line.ItemId == "fert.basic").Sum(line => line.Quantity));
        Assert.Equal(expectedPairs, lines.Where(line => line.ItemId == "seed.parsnip").Sum(line => line.Quantity));
        Assert.Equal(shortfall, plan.Shortfall);
    }

    [Fact]
    public void Store_hours_examples_match_vanilla_town_shops()
    {
        Assert.False(StoreHoursPolicy.IsOpen(Store.Pierre, 1000, dayOfMonth: 3));
        Assert.True(StoreHoursPolicy.IsOpen(Store.Pierre, 900, dayOfMonth: 1));
        Assert.False(StoreHoursPolicy.IsOpen(Store.Pierre, 1700, dayOfMonth: 1));
        Assert.True(StoreHoursPolicy.IsOpen(Store.Joja, 2200, dayOfMonth: 3));
        Assert.False(StoreHoursPolicy.IsOpen(Store.Joja, 2300, dayOfMonth: 3));
    }
}
