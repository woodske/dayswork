namespace Dayswork.Core.Crops;

public sealed record ShopStockSnapshot
{
    public Store Store { get; }
    public bool IsOpen { get; }
    public IReadOnlyDictionary<string, int> Stock { get; }

    /// <summary>Live unit price per item id. Empty when prices are unknown.</summary>
    public IReadOnlyDictionary<string, int> Prices { get; }

    public ShopStockSnapshot(Store store, bool isOpen, IReadOnlyDictionary<string, int>? stock)
        : this(store, isOpen, stock, null)
    {
    }

    public ShopStockSnapshot(
        Store store,
        bool isOpen,
        IReadOnlyDictionary<string, int>? stock,
        IReadOnlyDictionary<string, int>? prices)
    {
        Store = store;
        IsOpen = isOpen;
        Stock = (stock ?? new Dictionary<string, int>())
            .Where(kvp => !string.IsNullOrWhiteSpace(kvp.Key) && kvp.Value > 0)
            .OrderBy(kvp => kvp.Key, StringComparer.Ordinal)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.Ordinal);
        Prices = (prices ?? new Dictionary<string, int>())
            .Where(kvp => !string.IsNullOrWhiteSpace(kvp.Key) && kvp.Value > 0)
            .OrderBy(kvp => kvp.Key, StringComparer.Ordinal)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.Ordinal);
    }

    public int QuantityOf(string itemId) =>
        Stock.TryGetValue(itemId, out var quantity) ? quantity : 0;

    public int UnitPriceOf(string itemId) =>
        Prices.TryGetValue(itemId, out var price) ? price : 0;
}
