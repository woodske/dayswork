namespace Dayswork.Core.Crops;

public sealed class CropSupplyPlanner
{
    public int CompletableTiles(CropDescriptor crop, int viableTileCount, SupplyInventory inventory)
    {
        var cappedTiles = Math.Max(0, viableTileCount);
        var availableSeeds = inventory.QuantityOf(crop.SeedItemId);
        if (!crop.RequiresFertilizer)
            return Math.Min(cappedTiles, availableSeeds);

        var availableFertilizer = inventory.QuantityOf(crop.FertilizerItemId!);
        return Math.Min(cappedTiles, Math.Min(availableSeeds, availableFertilizer));
    }

    public IReadOnlyList<SupplyTarget> CalculatePurchaseTargets(
        CropDescriptor crop,
        int viableTileCount,
        SupplyInventory inventory,
        StorePreference storePreference,
        IReadOnlyList<ShopStockSnapshot>? stockSnapshots = null)
    {
        if (storePreference == StorePreference.InputChestOnly)
            return Array.Empty<SupplyTarget>();

        var targetTiles = Math.Max(0, viableTileCount);
        var availableSeeds = inventory.QuantityOf(crop.SeedItemId);
        var seedDeficit = Math.Max(0, targetTiles - availableSeeds);
        var targets = new List<SupplyTarget>();

        if (crop.RequiresFertilizer)
        {
            var fertilizerId = crop.FertilizerItemId!;
            var availableFertilizer = inventory.QuantityOf(fertilizerId);
            var fertilizerDeficit = Math.Max(0, targetTiles - availableFertilizer);
            var storeFertilizer = QuantityInStores(fertilizerId, stockSnapshots);

            if ((long)availableFertilizer + storeFertilizer <= 0)
                return Array.Empty<SupplyTarget>();

            var maxSeedPurchasesWithFertilizer = (int)Math.Min(
                (long)targetTiles,
                Math.Max(0L, (long)availableFertilizer + storeFertilizer - availableSeeds));
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
