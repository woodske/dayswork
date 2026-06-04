namespace Dayswork.Core.Crops;

public sealed record SupplyInventory
{
    public static SupplyInventory Empty { get; } = new(new Dictionary<string, int>());

    public IReadOnlyDictionary<string, int> Items { get; }

    public SupplyInventory(IReadOnlyDictionary<string, int>? items)
    {
        Items = (items ?? new Dictionary<string, int>())
            .Where(kvp => !string.IsNullOrWhiteSpace(kvp.Key) && kvp.Value > 0)
            .OrderBy(kvp => kvp.Key, StringComparer.Ordinal)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.Ordinal);
    }

    public int QuantityOf(string itemId) =>
        Items.TryGetValue(itemId, out var quantity) ? quantity : 0;
}
