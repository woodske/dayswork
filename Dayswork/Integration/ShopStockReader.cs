namespace Dayswork.Integration;

using System.Reflection;
using Dayswork.Core.Crops;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Internal;

/// <summary>
/// Live shop snapshot adapter for U-MC-06. It reads Stardew 1.6 <c>Data/Shops</c> once per store
/// per shift and maps stock plus unit prices into the pure shopping seams. Odd or conditional shop
/// rows are skipped rather than thrown; a failed read yields a closed/empty snapshot so the runtime
/// takes the planned skip-on-failure path.
/// </summary>
internal sealed class ShopStockReader
{
    private const string PierreShopId = "SeedShop";
    private const string JojaShopId = "Joja";

    private readonly IMonitor? _monitor;
    private readonly Dictionary<(Store Store, int Day, int Time, bool IncludeClosedStock), ShopStockSnapshot> _cache = new();

    public ShopStockReader(IMonitor? monitor = null) => _monitor = monitor;

    public void ResetForShift() => _cache.Clear();

    public IReadOnlyList<ShopStockSnapshot> ReadAll(int dayOfMonth, int timeOfDay, bool includeClosedStock = false) =>
        new[]
        {
            ReadStock(Store.Pierre, dayOfMonth, timeOfDay, includeClosedStock),
            ReadStock(Store.Joja, dayOfMonth, timeOfDay, includeClosedStock),
        };

    public ShopStockSnapshot ReadStock(Store store, int dayOfMonth, int timeOfDay, bool includeClosedStock = false)
    {
        var key = (store, dayOfMonth, timeOfDay, includeClosedStock);
        if (_cache.TryGetValue(key, out var cached))
            return cached;

        var isOpen = includeClosedStock
            ? StoreHoursPolicy.OpensAt(store, dayOfMonth) is not null
            : StoreHoursPolicy.IsOpen(store, timeOfDay, dayOfMonth);
        var stock = new Dictionary<string, int>(StringComparer.Ordinal);
        var prices = new Dictionary<string, int>(StringComparer.Ordinal);

        try
        {
            if (!TryReadShopBuilderStock(store, stock, prices, _monitor))
                ReadShopDataStock(store, stock, prices);
        }
        catch (Exception ex)
        {
            _monitor?.Log($"Dayswork shopping: could not read {store} stock ({ex.Message}).", LogLevel.Warn);
            isOpen = false;
            stock.Clear();
            prices.Clear();
        }

        cached = new ShopStockSnapshot(store, isOpen, stock, prices);
        _cache[key] = cached;
        return cached;
    }

    internal static bool TryGetLiveUnitPrice(Store store, string itemId, out int unitPrice)
    {
        unitPrice = 0;
        if (string.IsNullOrWhiteSpace(itemId))
            return false;

        try
        {
            var stock = new Dictionary<string, int>(StringComparer.Ordinal);
            var prices = new Dictionary<string, int>(StringComparer.Ordinal);
            if (!TryReadShopBuilderStock(store, stock, prices, monitor: null))
                ReadShopDataStock(store, stock, prices);

            return prices.TryGetValue(StripQualifier(itemId), out unitPrice) && unitPrice > 0;
        }
        catch
        {
            unitPrice = 0;
            return false;
        }
    }

    private static bool TryReadShopBuilderStock(
        Store store,
        IDictionary<string, int> stock,
        IDictionary<string, int> prices,
        IMonitor? monitor)
    {
        try
        {
            var liveStock = ShopBuilder.GetShopStock(ShopId(store));
            if (liveStock is null || liveStock.Count == 0)
                return false;

            foreach (var entry in liveStock)
                AddSalable(entry.Key, entry.Value, stock, prices);

            return stock.Count > 0;
        }
        catch (Exception ex)
        {
            monitor?.Log($"Dayswork shopping: ShopBuilder stock read for {store} failed ({ex.Message}); falling back to Data/Shops.", LogLevel.Trace);
            return false;
        }
    }

    private static void ReadShopDataStock(
        Store store,
        IDictionary<string, int> stock,
        IDictionary<string, int> prices)
    {
        var shops = DataLoader.Shops(Game1.content);
        if (!shops.TryGetValue(ShopId(store), out var shop) || shop.Items is null)
            return;

        foreach (var row in shop.Items)
        {
            AddItem(row.ItemId, row, stock, prices);
            if (row.RandomItemId is null)
                continue;

            foreach (var randomId in row.RandomItemId)
                AddItem(randomId, row, stock, prices);
        }
    }

    private static void AddSalable(
        ISalable salable,
        ItemStockInformation itemStock,
        IDictionary<string, int> stock,
        IDictionary<string, int> prices)
    {
        var quantity = itemStock.Stock <= 0 ? int.MaxValue : itemStock.Stock;
        var price = Math.Max(0, itemStock.Price);

        foreach (var id in SalableItemIds(salable).Distinct(StringComparer.Ordinal))
        {
            stock[id] = quantity;
            if (price > 0)
                prices[id] = price;
        }
    }

    private static IEnumerable<string> SalableItemIds(ISalable salable)
    {
        if (!string.IsNullOrWhiteSpace(salable.QualifiedItemId))
            yield return StripQualifier(salable.QualifiedItemId);

        if (salable is Item item && !string.IsNullOrWhiteSpace(item.ItemId))
            yield return StripQualifier(item.ItemId);
    }

    private static void AddItem(
        string? itemId,
        object row,
        IDictionary<string, int> stock,
        IDictionary<string, int> prices)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return;

        var normalized = StripQualifier(itemId);
        if (string.IsNullOrWhiteSpace(normalized))
            return;

        var quantity = ReadIntProperty(row, "AvailableStock", int.MaxValue);
        if (quantity <= 0)
            quantity = int.MaxValue;

        stock[normalized] = quantity;

        var price = ReadIntProperty(row, "Price", 0);
        if (price <= 0)
            price = ResolveObjectPrice(normalized);
        if (price > 0)
            prices[normalized] = price;
    }

    private static int ReadIntProperty(object source, string name, int fallback)
    {
        var property = source.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
        if (property is null)
            return fallback;

        var value = property.GetValue(source);
        return value switch
        {
            int i => i,
            long l when l <= int.MaxValue => (int)l,
            _ => fallback,
        };
    }

    private static int ResolveObjectPrice(string itemId)
    {
        try
        {
            foreach (var candidate in QualifiedItemCandidates(itemId).Distinct(StringComparer.Ordinal))
            {
                var data = ItemRegistry.GetDataOrErrorItem(candidate);
                var price = Math.Max(0, data.RawData is null ? 0 : ReadIntProperty(data.RawData, "Price", 0));
                if (price > 0)
                    return price;
            }

            return 0;
        }
        catch
        {
            return 0;
        }
    }

    private static string ShopId(Store store) => store == Store.Pierre ? PierreShopId : JojaShopId;

    private static IEnumerable<string> QualifiedItemCandidates(string itemId)
    {
        var qualified = ItemRegistry.QualifyItemId(itemId);
        if (!string.IsNullOrWhiteSpace(qualified))
            yield return qualified;

        if (!itemId.StartsWith("(", StringComparison.Ordinal))
            yield return $"(O){itemId}";

        yield return itemId;
    }

    private static string StripQualifier(string id)
    {
        var close = id.IndexOf(')');
        return id.StartsWith("(", StringComparison.Ordinal) && close >= 0 ? id[(close + 1)..] : id;
    }
}
