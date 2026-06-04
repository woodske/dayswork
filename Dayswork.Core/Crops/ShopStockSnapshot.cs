namespace Dayswork.Core.Crops;

public sealed record ShopStockSnapshot
{
    public Store Store { get; }
    public bool IsOpen { get; }
    public IReadOnlyDictionary<string, int> Stock { get; }

    public ShopStockSnapshot(Store store, bool isOpen, IReadOnlyDictionary<string, int>? stock)
    {
        Store = store;
        IsOpen = isOpen;
        Stock = (stock ?? new Dictionary<string, int>())
            .Where(kvp => !string.IsNullOrWhiteSpace(kvp.Key) && kvp.Value > 0)
            .OrderBy(kvp => kvp.Key, StringComparer.Ordinal)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.Ordinal);
    }

    public int QuantityOf(string itemId) =>
        Stock.TryGetValue(itemId, out var quantity) ? quantity : 0;
}
