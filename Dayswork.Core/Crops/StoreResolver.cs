namespace Dayswork.Core.Crops;

public sealed class StoreResolver
{
    private static readonly Store[] EitherOrder = { Store.Pierre, Store.Joja };

    public StoreResolution Resolve(
        StorePreference preference,
        string itemId,
        bool isFestivalDay,
        IReadOnlyList<ShopStockSnapshot>? stockSnapshots)
    {
        if (preference == StorePreference.InputChestOnly)
            return new StoreResolution(null, false, StoreClosedReason.ChestSupplyOnly, itemId);

        if (isFestivalDay)
            return new StoreResolution(null, false, StoreClosedReason.Festival, itemId);

        var snapshots = MergeSnapshots(stockSnapshots);

        return preference switch
        {
            StorePreference.Pierre => ResolvePreferred(Store.Pierre, Store.Joja, itemId, snapshots),
            StorePreference.Joja => ResolvePreferred(Store.Joja, Store.Pierre, itemId, snapshots),
            StorePreference.Either => ResolveEither(itemId, snapshots),
            _ => new StoreResolution(null, false, StoreClosedReason.ItemUnavailable, itemId),
        };
    }

    public IReadOnlyList<PurchaseLine> ResolvePurchaseLines(
        IReadOnlyList<SupplyTarget> targets,
        bool isFestivalDay,
        IReadOnlyList<ShopStockSnapshot>? stockSnapshots)
    {
        return targets
            .Select(target => (Target: target, Resolution: Resolve(target.StorePreference, target.ItemId, isFestivalDay, stockSnapshots)))
            .Where(item => item.Resolution.Store is not null)
            .Select(item => new PurchaseLine(item.Resolution.Store!.Value, item.Target.ItemId, item.Target.Quantity))
            .OrderBy(line => line.Store)
            .ThenBy(line => line.ItemId, StringComparer.Ordinal)
            .ToList()
            .AsReadOnly();
    }

    private static StoreResolution ResolvePreferred(
        Store preferred,
        Store fallback,
        string itemId,
        IReadOnlyDictionary<Store, ShopStockSnapshot> snapshots)
    {
        if (CanSell(preferred, itemId, snapshots))
            return new StoreResolution(preferred, false, null, itemId);

        if (CanSell(fallback, itemId, snapshots))
            return new StoreResolution(fallback, true, null, itemId);

        return AnyOpen(snapshots)
            ? new StoreResolution(null, false, StoreClosedReason.ItemUnavailable, itemId)
            : new StoreResolution(null, false, StoreClosedReason.AllStoresClosed, itemId);
    }

    private static StoreResolution ResolveEither(
        string itemId,
        IReadOnlyDictionary<Store, ShopStockSnapshot> snapshots)
    {
        foreach (var store in EitherOrder)
            if (CanSell(store, itemId, snapshots))
                return new StoreResolution(store, false, null, itemId);

        return AnyOpen(snapshots)
            ? new StoreResolution(null, false, StoreClosedReason.ItemUnavailable, itemId)
            : new StoreResolution(null, false, StoreClosedReason.AllStoresClosed, itemId);
    }

    private static bool CanSell(
        Store store,
        string itemId,
        IReadOnlyDictionary<Store, ShopStockSnapshot> snapshots) =>
        snapshots.TryGetValue(store, out var snapshot)
        && snapshot.IsOpen
        && snapshot.QuantityOf(itemId) > 0;

    private static bool AnyOpen(IReadOnlyDictionary<Store, ShopStockSnapshot> snapshots) =>
        snapshots.Values.Any(snapshot => snapshot.IsOpen);

    private static IReadOnlyDictionary<Store, ShopStockSnapshot> MergeSnapshots(IReadOnlyList<ShopStockSnapshot>? stockSnapshots) =>
        (stockSnapshots ?? Array.Empty<ShopStockSnapshot>())
            .GroupBy(snapshot => snapshot.Store)
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    var stock = group
                        .SelectMany(snapshot => snapshot.Stock)
                        .GroupBy(kvp => kvp.Key, StringComparer.Ordinal)
                        .ToDictionary(
                            stockGroup => stockGroup.Key,
                            stockGroup => stockGroup.Sum(kvp => kvp.Value),
                            StringComparer.Ordinal);

                    return new ShopStockSnapshot(group.Key, group.Any(snapshot => snapshot.IsOpen), stock);
                });
}
