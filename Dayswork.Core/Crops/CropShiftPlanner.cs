namespace Dayswork.Core.Crops;

using Dayswork.Core.Domain;

public sealed class CropShiftPlanner
{
    private readonly PlantingViabilityCalculator _viabilityCalculator;
    private readonly CropSupplyPlanner _supplyPlanner;
    private readonly StoreResolver _storeResolver;

    public CropShiftPlanner()
        : this(new PlantingViabilityCalculator(), new CropSupplyPlanner(), new StoreResolver())
    {
    }

    public CropShiftPlanner(
        PlantingViabilityCalculator viabilityCalculator,
        CropSupplyPlanner supplyPlanner,
        StoreResolver storeResolver)
    {
        _viabilityCalculator = viabilityCalculator;
        _supplyPlanner = supplyPlanner;
        _storeResolver = storeResolver;
    }

    public ManagedCropShiftPlan Plan(
        CropZoneAssignment assignment,
        FieldState fieldState,
        SupplyInventory inventory,
        IReadOnlyList<ShopStockSnapshot>? stockSnapshots,
        bool isFestivalDay,
        StorePreference? storePreferenceOverride = null)
    {
        var choice = ResolveChoice(assignment, fieldState);
        if (choice is null)
            return new ManagedCropShiftPlan(Array.Empty<TileAction>(), Array.Empty<TileAction>(), Array.Empty<PurchaseLine>());

        var zoneTiles = fieldState.Tiles
            .Where(tile => Contains(assignment.Zone, tile.Tile))
            .OrderBy(tile => tile.Tile.Y)
            .ThenBy(tile => tile.Tile.X)
            .ToList();

        // Viability gates planting AND its ground prep: if the crop can't mature before the season
        // ends, the farmhand neither tills nor plants (it would only be prepping ground it can't
        // use). Harvest and watering of EXISTING crops still run regardless.
        var isViable = _viabilityCalculator.IsPlantingViable(
            fieldState,
            choice.Crop,
            useFertilizer: choice.Crop.RequiresFertilizer);

        // Supply-independent maintenance: harvest ready crops + water existing live crops.
        var independent = BuildSupplyIndependentActions(assignment, fieldState.LocationName, zoneTiles);

        // Plant candidates are every non-crop tile (bare, tilled-empty, fresh debris, dead crop);
        // each is cleared/tilled/planted in one sequence, bounded by supply, so we never prep ground
        // we can't seed.
        var candidates = isViable
            ? zoneTiles.Where(tile => !tile.HasCrop).ToList()
            : new List<TileState>();

        // Planting is sized from the ACTUAL input-chest supply — shopping already ran up front, so
        // the chest is the source of truth (no projecting in seed the worker won't have on hand).
        var supplyDependent = BuildSupplyDependentActions(fieldState.LocationName, candidates, choice.Crop, inventory);

        // Purchases are unused by the planting path now (the up-front trip aggregates its own
        // manifest); kept so the DTO shape is stable. TODO: drop with a dedicated cleanup.
        var preFertilizedCount = choice.Crop.RequiresFertilizer
            ? candidates.Count(tile => tile.HasFertilizer)
            : 0;
        var supplyTargets = _supplyPlanner.CalculatePurchaseTargets(
            choice.Crop,
            candidates.Count,
            inventory,
            storePreferenceOverride ?? choice.StorePreference,
            stockSnapshots,
            preFertilizedCount);
        var purchases = _storeResolver.ResolvePurchaseLines(supplyTargets, isFestivalDay, stockSnapshots);

        return new ManagedCropShiftPlan(independent, supplyDependent, purchases);
    }

    private static SeasonCropChoice? ResolveChoice(CropZoneAssignment assignment, FieldState fieldState)
    {
        if (assignment.Mode == CropAssignmentMode.SeasonAgnostic)
            return assignment.Choices.FirstOrDefault();

        return assignment.Choices.FirstOrDefault(choice => choice.Season == fieldState.Date.Season);
    }

    // Supply-independent maintenance for tiles that already hold a crop: harvest what's ready and
    // water what's growing. Ground prep (clear/till) is NOT here — it belongs to a tile's planting
    // sequence so it only runs when there's seed for that tile.
    private static IReadOnlyList<TileAction> BuildSupplyIndependentActions(
        CropZoneAssignment assignment,
        string locationName,
        IReadOnlyList<TileState> tiles)
    {
        var actions = new List<TileAction>();
        var harvestProvenance = ManagedCropOutputRouter.ProvenanceFor(assignment);
        foreach (var tile in tiles)
        {
            if (tile.ReadyToHarvest)
                actions.Add(new TileAction(
                    locationName,
                    tile.Tile,
                    ManagedCropActionKind.Harvest,
                    OutputProvenance: harvestProvenance));
            if (tile.HasCrop && !tile.IsWatered)
                actions.Add(new TileAction(locationName, tile.Tile, ManagedCropActionKind.Water));
        }

        return actions.AsReadOnly();
    }

    // Full per-tile flow, bounded by what the chest actually holds: for each candidate we can seed,
    // clear debris (if any) → till (if untilled) → fertilize (if required) → plant → water. A tile
    // we lack the seed/fertilizer for emits nothing, so the worker never preps ground it can't plant.
    private static IReadOnlyList<TileAction> BuildSupplyDependentActions(
        string locationName,
        IReadOnlyList<TileState> candidates,
        CropDescriptor crop,
        SupplyInventory inventory)
    {
        var remainingSeeds = inventory.QuantityOf(crop.SeedItemId);
        var remainingFertilizer = crop.RequiresFertilizer
            ? inventory.QuantityOf(crop.FertilizerItemId!)
            : 0;
        var actions = new List<TileAction>();

        foreach (var tile in candidates)
        {
            if (remainingSeeds <= 0)
                break;

            var tileNeedsFertilizer = crop.RequiresFertilizer && !tile.HasFertilizer;
            if (tileNeedsFertilizer && remainingFertilizer <= 0)
                continue; // save the seed for a tile we can fully prepare (a later pre-fertilized one)

            if (tile.HasDebris)
                actions.Add(new TileAction(locationName, tile.Tile, ManagedCropActionKind.ClearDebris));
            if (!tile.IsTilled)
                actions.Add(new TileAction(locationName, tile.Tile, ManagedCropActionKind.Till, RequiresDiggable: true));

            if (tileNeedsFertilizer)
            {
                actions.Add(new TileAction(locationName, tile.Tile, ManagedCropActionKind.Fertilize, crop.FertilizerItemId));
                remainingFertilizer--;
            }

            actions.Add(new TileAction(locationName, tile.Tile, ManagedCropActionKind.PlantSeed, crop.SeedItemId));
            if (!tile.IsWatered)
                actions.Add(new TileAction(locationName, tile.Tile, ManagedCropActionKind.Water));
            remainingSeeds--;
        }

        return actions.AsReadOnly();
    }

    private static bool Contains(Zone zone, TileCoord tile) =>
        tile.X >= zone.TopLeft.X
        && tile.X <= zone.BottomRight.X
        && tile.Y >= zone.TopLeft.Y
        && tile.Y <= zone.BottomRight.Y;
}
