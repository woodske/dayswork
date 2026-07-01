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

    // Backs the up-front consolidated shopping trip: demand from every managed location is summed
    // against ONE shared input-chest reservoir, so the single seed in the chest is subtracted once
    // from the combined demand (3), not once per location (which would leave 2×(2-1) = 2).
    [Fact]
    public void Multi_location_manifest_sums_demand_against_one_shared_chest_reservoir()
    {
        var crop = Parsnip();
        var farm = new CropZoneAssignment(
            new Zone("Farm", new TileCoord(0, 0), new TileCoord(1, 0)),
            CropAssignmentMode.Seasonal,
            new[] { new SeasonCropChoice(Season.Spring, crop, StorePreference.Pierre) });
        var shed = new CropZoneAssignment(
            new Zone("Greenhouse", new TileCoord(0, 0), new TileCoord(1, 0)),
            CropAssignmentMode.SeasonAgnostic,
            new[] { new SeasonCropChoice(Season.Spring, crop, StorePreference.Pierre) });

        var manifest = new ShiftSupplyAggregator().BuildManifest(
            new CropPlan(new[] { farm, shed }),
            new[]
            {
                PlantableField("Farm", isSeasonAgnostic: false),
                PlantableField("Greenhouse", isSeasonAgnostic: true),
            },
            new SupplyInventory(new Dictionary<string, int> { ["seed.parsnip"] = 1 }),
            StorePreference.Pierre,
            ParsnipStock(),
            isFestivalDay: false);

        var line = Assert.Single(Assert.Single(manifest.Groups).Lines);
        Assert.Equal("seed.parsnip", line.ItemId);
        Assert.Equal(3, line.Quantity);
    }

    // Each assignment is sized against ITS OWN location's field: the greenhouse tiles already hold
    // crops (nothing to plant), so only the farm zone's two tiles drive the purchase.
    [Fact]
    public void Multi_location_manifest_sizes_each_zone_against_its_own_field()
    {
        var crop = Parsnip();
        var farm = new CropZoneAssignment(
            new Zone("Farm", new TileCoord(0, 0), new TileCoord(1, 0)),
            CropAssignmentMode.Seasonal,
            new[] { new SeasonCropChoice(Season.Spring, crop, StorePreference.Pierre) });
        var shed = new CropZoneAssignment(
            new Zone("Greenhouse", new TileCoord(0, 0), new TileCoord(1, 0)),
            CropAssignmentMode.SeasonAgnostic,
            new[] { new SeasonCropChoice(Season.Spring, crop, StorePreference.Pierre) });

        // Greenhouse tiles: HasCrop=true → not plantable this shift.
        var shedField = new FieldState(
            "Greenhouse",
            new GameDate(1, Season.Spring, 1),
            isSeasonAgnosticLocation: true,
            new[]
            {
                new TileState(new TileCoord(0, 0), false, true, false, false, false, false),
                new TileState(new TileCoord(1, 0), false, true, false, false, false, false),
            });

        var manifest = new ShiftSupplyAggregator().BuildManifest(
            new CropPlan(new[] { farm, shed }),
            new[] { PlantableField("Farm", isSeasonAgnostic: false), shedField },
            new SupplyInventory(new Dictionary<string, int>()),
            StorePreference.Pierre,
            ParsnipStock(),
            isFestivalDay: false);

        var line = Assert.Single(Assert.Single(manifest.Groups).Lines);
        Assert.Equal("seed.parsnip", line.ItemId);
        Assert.Equal(2, line.Quantity);
    }

    private static CropDescriptor Parsnip() => new(
        "crop.parsnip",
        "seed.parsnip",
        fertilizerItemId: null,
        daysToFirstHarvest: 4,
        fertilizedDaysToFirstHarvest: null,
        regrowDays: null,
        new[] { Season.Spring });

    private static FieldState PlantableField(string locationName, bool isSeasonAgnostic) => new(
        locationName,
        new GameDate(1, Season.Spring, 1),
        isSeasonAgnostic,
        new[]
        {
            new TileState(new TileCoord(0, 0), false, false, false, false, false, false),
            new TileState(new TileCoord(1, 0), false, false, false, false, false, false),
        });

    private static ShopStockSnapshot[] ParsnipStock() => new[]
    {
        new ShopStockSnapshot(
            Store.Pierre,
            true,
            new Dictionary<string, int> { ["seed.parsnip"] = 99 },
            new Dictionary<string, int> { ["seed.parsnip"] = 20 }),
    };

    // Fertilizer (unit 20) is ordered first within the group, so under a wallet shortfall it is
    // funded before its seed (unit 30) — a seed is never bought that can't be fertilized.
    [Theory]
    [InlineData(100, 2, 2, false)] // affords both fully
    [InlineData(60, 2, 0, true)]   // affords all fertilizer, no seeds left
    [InlineData(40, 2, 0, true)]   // exactly all fertilizer
    [InlineData(20, 1, 0, true)]   // partial fertilizer only
    [InlineData(0, 0, 0, true)]
    public void Affordability_funds_fertilizer_before_seeds(int wallet, int expectedFert, int expectedSeed, bool shortfall)
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

        Assert.Equal(expectedFert, lines.Where(line => line.ItemId == "fert.basic").Sum(line => line.Quantity));
        Assert.Equal(expectedSeed, lines.Where(line => line.ItemId == "seed.parsnip").Sum(line => line.Quantity));
        Assert.Equal(shortfall, plan.Shortfall);
    }

    // Regression: a single aggregated fertilizer line (demand summed across two fert-requiring
    // zones) must not be clamped down to the first seed line it sits next to. Previously the clamp
    // paired the 36-fertilizer line with the 9-seed melon line and bought only 9 fertilizer, even
    // though the wallet could afford everything.
    [Fact]
    public void Aggregated_fertilizer_line_not_clamped_to_first_seed_when_affordable()
    {
        var manifest = new ShiftPurchaseManifest(
            new[]
            {
                new StorePurchaseGroup(
                    Store.Pierre,
                    new[]
                    {
                        new ManifestLine("fert.basic", 36, 100, IsFertilizer: true),
                        new ManifestLine("seed.melon", 9, 80, IsFertilizer: false),
                        new ManifestLine("seed.squash", 18, 90, IsFertilizer: false),
                    }),
            },
            Array.Empty<string>());

        var plan = new PurchaseAffordabilityCalculator().ClampToWallet(manifest, walletGold: 100_000);
        var lines = plan.Groups.SelectMany(group => group.Lines).ToList();

        Assert.Equal(36, lines.Single(line => line.ItemId == "fert.basic").Quantity);
        Assert.Equal(9, lines.Single(line => line.ItemId == "seed.melon").Quantity);
        Assert.Equal(18, lines.Single(line => line.ItemId == "seed.squash").Quantity);
        Assert.False(plan.Shortfall);
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
