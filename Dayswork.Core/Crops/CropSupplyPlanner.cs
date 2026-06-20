namespace Dayswork.Core.Crops;

public sealed class CropSupplyPlanner
{
    public int CompletableTiles(CropDescriptor crop, int viableTileCount, SupplyInventory inventory, int preFertilizedTileCount = 0)
    {
        var cappedTiles = Math.Max(0, viableTileCount);
        var availableSeeds = inventory.QuantityOf(crop.SeedItemId);
        if (!crop.RequiresFertilizer)
            return Math.Min(cappedTiles, availableSeeds);

        var availableFertilizer = inventory.QuantityOf(crop.FertilizerItemId!);
        // Pre-fertilized tiles consume only a seed; remaining tiles need seed + fertilizer.
        var preFertilized = Math.Min(preFertilizedTileCount, cappedTiles);
        var fromPreFertilized = Math.Min(preFertilized, availableSeeds);
        var seedsLeft = availableSeeds - fromPreFertilized;
        var unfertilizedTiles = cappedTiles - preFertilized;
        var fromUnfertilized = Math.Min(unfertilizedTiles, Math.Min(seedsLeft, availableFertilizer));
        return fromPreFertilized + fromUnfertilized;
    }

    public IReadOnlyList<SupplyTarget> CalculatePurchaseTargets(
        CropDescriptor crop,
        int viableTileCount,
        SupplyInventory inventory,
        StorePreference storePreference,
        IReadOnlyList<ShopStockSnapshot>? stockSnapshots = null,
        int preFertilizedTileCount = 0)
    {
        if (storePreference == StorePreference.InputChestOnly)
            return Array.Empty<SupplyTarget>();

        var targetTiles = Math.Max(0, viableTileCount);
        var availableSeeds = inventory.QuantityOf(crop.SeedItemId);
        var seedDeficit = Math.Max(0, targetTiles - availableSeeds);
        var targets = new List<SupplyTarget>();

        if (crop.RequiresFertilizer)
        {
            var unfertilizedTiles = Math.Max(0, targetTiles - preFertilizedTileCount);
            var fertilizerId = crop.FertilizerItemId!;
            var availableFertilizer = inventory.QuantityOf(fertilizerId);
            var fertilizerDeficit = Math.Max(0, unfertilizedTiles - availableFertilizer);
            var storeFertilizer = QuantityInStores(fertilizerId, stockSnapshots);

            // Bail only when there is genuinely no fertilizer capacity for tiles that need it.
            if (unfertilizedTiles > 0 && (long)availableFertilizer + storeFertilizer <= 0)
                return Array.Empty<SupplyTarget>();

            var maxSeedPurchasesWithFertilizer = (int)Math.Min(
                (long)targetTiles,
                Math.Max(0L, (long)preFertilizedTileCount + availableFertilizer + storeFertilizer - availableSeeds));
            seedDeficit = Math.Min(seedDeficit, maxSeedPurchasesWithFertilizer);

            if (fertilizerDeficit > 0)
                targets.Add(new SupplyTarget(fertilizerId, fertilizerDeficit, storePreference));
        }

        if (seedDeficit > 0)
            targets.Add(new SupplyTarget(crop.SeedItemId, seedDeficit, storePreference));

        return targets
            .Where(target => target.Quantity > 0)
            .OrderBy(target => target.ItemId, StringComparer.Ordinal)
            .ToList()
            .AsReadOnly();
    }

    public bool HasAtomicSuppliesForTile(CropDescriptor crop, SupplyInventory inventory) =>
        inventory.QuantityOf(crop.SeedItemId) > 0
        && (!crop.RequiresFertilizer || inventory.QuantityOf(crop.FertilizerItemId!) > 0);

    private static long QuantityInStores(string itemId, IReadOnlyList<ShopStockSnapshot>? stockSnapshots) =>
        (stockSnapshots ?? Array.Empty<ShopStockSnapshot>())
            .Where(snapshot => snapshot.IsOpen)
            .Sum(snapshot => (long)snapshot.QuantityOf(itemId));
}
