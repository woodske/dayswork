namespace Dayswork.Core.Crops;

using Dayswork.Core.Domain;

/// <summary>
/// Pure shift-level supply aggregator. Builds one <see cref="ShiftPurchaseManifest"/>
/// for the whole shift by sizing each managed zone's purchase from its planned plantable tiles
///, summing demand across zones against a single shared input-chest reservoir, then
/// resolving each item to a store with the global preference and live stock+prices.
/// </summary>
public sealed class ShiftSupplyAggregator
{
    private readonly PlantingViabilityCalculator _viability;
    private readonly CropSupplyPlanner _supplyPlanner;
    private readonly StoreResolver _storeResolver;

    public ShiftSupplyAggregator()
        : this(new PlantingViabilityCalculator(), new CropSupplyPlanner(), new StoreResolver())
    {
    }

    public ShiftSupplyAggregator(
        PlantingViabilityCalculator viability,
        CropSupplyPlanner supplyPlanner,
        StoreResolver storeResolver)
    {
        _viability = viability;
        _supplyPlanner = supplyPlanner;
        _storeResolver = storeResolver;
    }

    public ShiftPurchaseManifest BuildManifest(
        CropPlan plan,
        FieldState field,
        SupplyInventory chestInventory,
        StorePreference globalPreference,
        IReadOnlyList<ShopStockSnapshot>? liveStock,
        bool isFestivalDay,
        Action<string>? debugLog = null) =>
        BuildManifest(
            plan,
            new[] { field },
            chestInventory,
            globalPreference,
            liveStock,
            isFestivalDay,
            debugLog);

    /// <summary>
    /// Builds one manifest spanning several managed locations at once: each assignment is sized
    /// against its own location's <see cref="FieldState"/> while all zones draw from the single
    /// shared input-chest reservoir, so two locations needing the same seed don't both assume the
    /// full chest stock. Backs the up-front consolidated shopping trip.
    /// </summary>
    public ShiftPurchaseManifest BuildManifest(
        CropPlan plan,
        IReadOnlyList<FieldState> fields,
        SupplyInventory chestInventory,
        StorePreference globalPreference,
        IReadOnlyList<ShopStockSnapshot>? liveStock,
        bool isFestivalDay,
        Action<string>? debugLog = null)
    {
        if (plan is null || !plan.IsEnabled || fields is null || fields.Count == 0)
            return ShiftPurchaseManifest.Empty;

        var fieldByLocation = new Dictionary<string, FieldState>(StringComparer.Ordinal);
        foreach (var f in fields)
        {
            if (f is not null)
                fieldByLocation[f.LocationName] = f;
        }

        // Working copy of the input chest, decremented per zone so two zones sharing a seed or
        // fertilizer don't both assume the full chest stock.
        var working = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var kvp in chestInventory.Items)
            working[kvp.Key] = kvp.Value;

        var aggregated = new Dictionary<string, int>(StringComparer.Ordinal);
        var fertilizerItems = new HashSet<string>(StringComparer.Ordinal);

        foreach (var assignment in plan.Assignments)
        {
            if (!fieldByLocation.TryGetValue(assignment.Zone.LocationName, out var field))
                continue;

            var choice = ResolveChoice(assignment, field);
            if (choice is null)
            {
                debugLog?.Invoke($"zone ({assignment.Zone.TopLeft.X},{assignment.Zone.TopLeft.Y})-({assignment.Zone.BottomRight.X},{assignment.Zone.BottomRight.Y}): no choice for season {field.Date.Season}");
                continue;
            }

            var crop = choice.Crop;
            var isViable = _viability.IsPlantingViable(field, crop, useFertilizer: crop.RequiresFertilizer);
            var planned = PlannedPlantableTileCounter.CountPlantable(field, assignment, choice, isViable);
            if (planned <= 0)
            {
                debugLog?.Invoke($"zone ({assignment.Zone.TopLeft.X},{assignment.Zone.TopLeft.Y})-({assignment.Zone.BottomRight.X},{assignment.Zone.BottomRight.Y}): seed={crop.SeedItemId} isViable={isViable} planned=0, skipped");
                continue;
            }

            var preFertilized = crop.RequiresFertilizer
                ? PlannedPlantableTileCounter.CountPreFertilized(field, assignment, choice)
                : 0;

            var workingInventory = new SupplyInventory(working);
            var targets = _supplyPlanner.CalculatePurchaseTargets(
                crop, planned, workingInventory, globalPreference, liveStock, preFertilized);

            debugLog?.Invoke($"zone ({assignment.Zone.TopLeft.X},{assignment.Zone.TopLeft.Y})-({assignment.Zone.BottomRight.X},{assignment.Zone.BottomRight.Y}): seed={crop.SeedItemId} fert={crop.FertilizerItemId} isViable={isViable} planned={planned} preFertilized={preFertilized} targets=[{string.Join(", ", targets.Select(t => $"{t.ItemId}:{t.Quantity}"))}]");

            foreach (var target in targets)
            {
                aggregated.TryGetValue(target.ItemId, out var existing);
                aggregated[target.ItemId] = existing + target.Quantity;
            }

            if (crop.RequiresFertilizer)
                fertilizerItems.Add(crop.FertilizerItemId!);

            // Consume what this zone will draw from the shared chest so later zones see the remainder.
            ConsumeFromWorking(working, crop.SeedItemId, planned);
            if (crop.RequiresFertilizer)
                ConsumeFromWorking(working, crop.FertilizerItemId!, Math.Max(0, planned - preFertilized));
        }

        if (aggregated.Count == 0)
            return ShiftPurchaseManifest.Empty;

        var supplyTargets = aggregated
            .Select(kvp => new SupplyTarget(kvp.Key, kvp.Value, globalPreference))
            .ToList();

        var lines = _storeResolver.ResolvePurchaseLines(supplyTargets, isFestivalDay, liveStock);

        var resolvedItemIds = new HashSet<string>(lines.Select(line => line.ItemId), StringComparer.Ordinal);
        var unavailable = aggregated.Keys.Where(id => !resolvedItemIds.Contains(id)).ToList();

        var groups = lines
            .GroupBy(line => line.Store)
            .Select(group => new StorePurchaseGroup(
                group.Key,
                group
                    .Select(line => new ManifestLine(
                        line.ItemId,
                        line.Quantity,
                        PriceOf(group.Key, line.ItemId, liveStock),
                        fertilizerItems.Contains(line.ItemId)))
                    .ToList()))
            .ToList();

        var ordered = OrderGroupsPreferredFirst(groups, globalPreference);
        return new ShiftPurchaseManifest(ordered, unavailable);
    }

    private static SeasonCropChoice? ResolveChoice(CropZoneAssignment assignment, FieldState field)
    {
        if (assignment.Mode == CropAssignmentMode.SeasonAgnostic)
            return assignment.Choices.FirstOrDefault();

        return assignment.Choices.FirstOrDefault(choice => choice.Season == field.Date.Season);
    }

    private static void ConsumeFromWorking(IDictionary<string, int> working, string itemId, int planned)
    {
        if (!working.TryGetValue(itemId, out var available))
            return;

        var consumed = Math.Min(planned, available);
        var remaining = available - consumed;
        if (remaining > 0)
            working[itemId] = remaining;
        else
            working.Remove(itemId);
    }

    private static int PriceOf(Store store, string itemId, IReadOnlyList<ShopStockSnapshot>? liveStock)
    {
        if (liveStock is null)
            return 0;

        foreach (var snapshot in liveStock)
        {
            if (snapshot.Store != store)
                continue;

            var price = snapshot.UnitPriceOf(itemId);
            if (price > 0)
                return price;
        }

        return 0;
    }

    private static IReadOnlyList<StorePurchaseGroup> OrderGroupsPreferredFirst(
        IReadOnlyList<StorePurchaseGroup> groups,
        StorePreference preference)
    {
        Store? preferred = preference switch
        {
            StorePreference.Pierre => Store.Pierre,
            StorePreference.Joja => Store.Joja,
            _ => null,
        };

        return groups
            .OrderByDescending(group => preferred is not null && group.Store == preferred.Value)
            .ThenBy(group => group.Store)
            .ToList()
            .AsReadOnly();
    }
}
