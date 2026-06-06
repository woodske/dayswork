namespace Dayswork.Tests.Generators;

using Dayswork.Core.Crops;
using Dayswork.Core.Domain;
using FsCheck;

public sealed record ViabilityCase(FieldState FieldState, CropDescriptor Crop);

public sealed record SupplyCase(CropDescriptor Crop, int ViableTiles, SupplyInventory Inventory);

public sealed record StoreCase(StorePreference Preference, string ItemId, bool IsFestival, IReadOnlyList<ShopStockSnapshot> Stock);

public sealed record ShiftPlanCase(CropZoneAssignment Assignment, FieldState FieldState, SupplyInventory Inventory, IReadOnlyList<ShopStockSnapshot> Stock);

public static class ManageCropsGen
{
    private static readonly string[] CropIds = { "crop.parsnip", "crop.blueberry", "crop.corn", "crop.ancient" };
    private static readonly string[] SeedIds = { "seed.parsnip", "seed.blueberry", "seed.corn", "seed.ancient" };
    private static readonly string[] FertilizerIds = { "fert.basic", "fert.quality" };

    public static Arbitrary<CropPlan> CropPlan() =>
        (from count in Gen.Choose(0, 4)
         from assignments in Gen.ListOf(count, CropZoneAssignment().Generator)
         select new CropPlan(assignments.ToList()))
        .ToArbitrary();

    public static Arbitrary<CropZoneAssignment> CropZoneAssignment() =>
        (from zone in ZoneGen()
         from mode in Gen.Elements(CropAssignmentMode.Seasonal, CropAssignmentMode.SeasonAgnostic)
         from choice in SeasonCropChoice().Generator
         from hasGroupId in Arb.Generate<bool>()
         from groupIndex in Gen.Choose(1, 4)
         select new CropZoneAssignment(
             zone,
             mode,
             new[] { choice },
             groupId: hasGroupId ? $"group-{groupIndex}" : null))
        .ToArbitrary();

    public static Arbitrary<SeasonCropChoice> SeasonCropChoice() =>
        (from season in SeasonGen()
         from crop in CropDescriptor().Generator
         from preference in StorePreferenceGen()
         from autoReplant in Arb.Generate<bool>()
         select new SeasonCropChoice(season, crop, preference, autoReplant: autoReplant))
        .ToArbitrary();

    public static Arbitrary<CropDescriptor> CropDescriptor() =>
        (from cropIndex in Gen.Choose(0, CropIds.Length - 1)
         from days in Gen.Choose(1, 13)
         from useFertilizer in Arb.Generate<bool>()
         from fertilizedDays in Gen.Choose(1, 12)
         from fertilizer in Gen.Elements(FertilizerIds)
         from useRegrow in Arb.Generate<bool>()
         from regrow in Gen.Choose(2, 7)
         from seasons in Gen.NonEmptyListOf(SeasonGen())
         select new CropDescriptor(
             CropIds[cropIndex],
             SeedIds[cropIndex],
             useFertilizer ? fertilizer : null,
             days,
             useFertilizer ? fertilizedDays : null,
             useRegrow ? regrow : null,
             seasons.Distinct().ToList()))
        .ToArbitrary();

    public static Arbitrary<SupplyInventory> SupplyInventory() =>
        ItemQuantities().Select(items => new SupplyInventory(items)).ToArbitrary();

    public static Arbitrary<ShopStockSnapshot> ShopStockSnapshot() =>
        (from store in Gen.Elements(Store.Pierre, Store.Joja)
         from isOpen in Arb.Generate<bool>()
         from stock in ItemQuantities()
         select new ShopStockSnapshot(store, isOpen, stock))
        .ToArbitrary();

    public static Arbitrary<FieldState> FieldState() =>
        (from location in Gen.Elements("Farm", "Greenhouse", "GrandpaShed")
         from day in Gen.Choose(1, 28)
         from season in SeasonGen()
         from isSeasonAgnostic in Arb.Generate<bool>()
         from tileCount in Gen.Choose(1, 8)
         from tiles in Gen.ListOf(tileCount, TileStateGen())
         select new FieldState(location, new GameDate(day, season, 1), isSeasonAgnostic, tiles.ToList()))
        .ToArbitrary();

    public static Arbitrary<ViabilityCase> ViabilityCase() =>
        (from field in FieldState().Generator
         from crop in CropDescriptor().Generator
         select new ViabilityCase(field, crop))
        .ToArbitrary();

    public static Arbitrary<SupplyCase> SupplyCase() =>
        (from crop in CropDescriptor().Generator
         from viableTiles in Gen.Choose(0, 24)
         from inventory in SupplyInventory().Generator
         select new SupplyCase(crop, viableTiles, inventory))
        .ToArbitrary();

    public static Arbitrary<StoreCase> StoreCase() =>
        (from preference in StorePreferenceGen()
         from itemId in Gen.Elements(SeedIds.Concat(FertilizerIds).ToArray())
         from isFestival in Arb.Generate<bool>()
         from stockCount in Gen.Choose(0, 2)
         from stock in Gen.ListOf(stockCount, ShopStockSnapshot().Generator)
         select new StoreCase(preference, itemId, isFestival, stock.ToList()))
        .ToArbitrary();

    public static Arbitrary<ShiftPlanCase> ShiftPlanCase() =>
        (from assignment in CropZoneAssignment().Generator
         from inventory in SupplyInventory().Generator
         from stockCount in Gen.Choose(0, 2)
         from stock in Gen.ListOf(stockCount, ShopStockSnapshot().Generator)
         let field = new FieldState(
             assignment.Zone.LocationName,
             new GameDate(1, assignment.Choices[0].Season, 1),
             assignment.Mode == CropAssignmentMode.SeasonAgnostic,
             TilesInside(assignment.Zone))
         select new ShiftPlanCase(assignment, field, inventory, stock.ToList()))
        .ToArbitrary();

    private static Gen<IReadOnlyDictionary<string, int>> ItemQuantities() =>
        (from count in Gen.Choose(0, 6)
         from items in Gen.ListOf(
             count,
             from itemId in Gen.Elements(SeedIds.Concat(FertilizerIds).ToArray())
             from quantity in Gen.Choose(0, 24)
             select new KeyValuePair<string, int>(itemId, quantity))
         select items)
            .Select(items => (IReadOnlyDictionary<string, int>)items
                .GroupBy(item => item.Key)
                .ToDictionary(group => group.Key, group => group.Sum(item => item.Value), StringComparer.Ordinal));

    private static Gen<TileState> TileStateGen() =>
        from x in Gen.Choose(0, 6)
        from y in Gen.Choose(0, 6)
        from readyToHarvest in Arb.Generate<bool>()
        from hasCrop in Arb.Generate<bool>()
        from hasDebris in Arb.Generate<bool>()
        from isTilled in Arb.Generate<bool>()
        from hasFertilizer in Arb.Generate<bool>()
        from isWatered in Arb.Generate<bool>()
        select new TileState(new TileCoord(x, y), readyToHarvest, hasCrop, hasDebris, isTilled, hasFertilizer, isWatered);

    private static IReadOnlyList<TileState> TilesInside(Zone zone) =>
        Enumerable.Range(zone.TopLeft.X, Math.Max(1, zone.BottomRight.X - zone.TopLeft.X + 1))
            .Take(4)
            .Select(x => new TileState(new TileCoord(x, zone.TopLeft.Y), false, false, false, false, false, false))
            .ToList()
            .AsReadOnly();

    private static Gen<Zone> ZoneGen() =>
        from x in Gen.Choose(0, 4)
        from y in Gen.Choose(0, 4)
        from width in Gen.Choose(0, 3)
        from height in Gen.Choose(0, 3)
        select new Zone("Farm", new TileCoord(x, y), new TileCoord(x + width, y + height));

    private static Gen<Season> SeasonGen() =>
        Gen.Elements(Season.Spring, Season.Summer, Season.Fall, Season.Winter);

    private static Gen<StorePreference> StorePreferenceGen() =>
        Gen.Elements(StorePreference.InputChestOnly, StorePreference.Pierre, StorePreference.Joja, StorePreference.Either);
}
