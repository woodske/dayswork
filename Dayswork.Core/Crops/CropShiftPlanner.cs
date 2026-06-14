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

        // Viability gates planting AND ground preparation: if the crop cannot mature before the
        // season ends, the farmhand neither tills nor plants the zone's bare tiles this shift
        // (it would only be tilling ground it can't use). Harvest/water of existing crops and
        // debris clearing still run.
        var isViable = _viabilityCalculator.IsPlantingViable(
            fieldState,
            choice.Crop,
            useFertilizer: choice.Crop.RequiresFertilizer);

        var independent = BuildSupplyIndependentActions(assignment, fieldState.LocationName, zoneTiles, allowTill: isViable);
        var candidates = isViable
            ? zoneTiles.Where(tile => tile.CanAcceptSeed).ToList()
            : new List<TileState>();

        var supplyTargets = _supplyPlanner.CalculatePurchaseTargets(
            choice.Crop,
            candidates.Count,
            inventory,
            storePreferenceOverride ?? choice.StorePreference,
            stockSnapshots);
        var purchases = _storeResolver.ResolvePurchaseLines(supplyTargets, isFestivalDay, stockSnapshots);
        var projectedInventory = ProjectInventory(inventory, purchases);
        var supplyDependent = BuildSupplyDependentActions(fieldState.LocationName, candidates, choice.Crop, projectedInventory);

        return new ManagedCropShiftPlan(independent, supplyDependent, purchases);
    }

    private static SeasonCropChoice? ResolveChoice(CropZoneAssignment assignment, FieldState fieldState)
    {
        if (assignment.Mode == CropAssignmentMode.SeasonAgnostic)
            return assignment.Choices.FirstOrDefault();

        return assignment.Choices.FirstOrDefault(choice => choice.Season == fieldState.Date.Season);
    }

    private static IReadOnlyList<TileAction> BuildSupplyIndependentActions(
        CropZoneAssignment assignment,
        string locationName,
        IReadOnlyList<TileState> tiles,
        bool allowTill)
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
            if (tile.HasDebris)
                actions.Add(new TileAction(locationName, tile.Tile, ManagedCropActionKind.ClearDebris));
            if (allowTill && !tile.HasCrop && !tile.IsTilled)
                actions.Add(new TileAction(locationName, tile.Tile, ManagedCropActionKind.Till, RequiresDiggable: true));
            if (tile.HasCrop && !tile.IsWatered)
                actions.Add(new TileAction(locationName, tile.Tile, ManagedCropActionKind.Water));
        }

        return actions.AsReadOnly();
    }

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

            if (crop.RequiresFertilizer && remainingFertilizer <= 0)
                break;

            if (crop.RequiresFertilizer)
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

    private static SupplyInventory ProjectInventory(SupplyInventory inventory, IReadOnlyList<PurchaseLine> purchases)
    {
        var projected = inventory.Items.ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.Ordinal);
        foreach (var purchase in purchases)
        {
            projected.TryGetValue(purchase.ItemId, out var existing);
            projected[purchase.ItemId] = existing + purchase.Quantity;
        }

        return new SupplyInventory(projected);
    }

    private static bool Contains(Zone zone, TileCoord tile) =>
        tile.X >= zone.TopLeft.X
        && tile.X <= zone.BottomRight.X
        && tile.Y >= zone.TopLeft.Y
        && tile.Y <= zone.BottomRight.Y;
}
