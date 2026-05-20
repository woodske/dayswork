namespace Dayswork.Core.Inventory;

public sealed class ItemBuffer : IItemBuffer
{
    private readonly List<(string itemId, int quantity)> _items = new();

    public bool IsEmpty => _items.Count == 0;

    public void Add(string itemId, int quantity)
    {
        if (string.IsNullOrEmpty(itemId)) throw new ArgumentException("itemId must be non-empty.", nameof(itemId));
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity), "quantity must be positive.");
        _items.Add((itemId, quantity));
    }

    public IReadOnlyList<(string itemId, int quantity)> TakeAll()
    {
        var result = _items.ToList().AsReadOnly();
        _items.Clear();
        return result;
    }

    public IReadOnlyList<(string itemId, int quantity)> Snapshot() =>
        _items.ToList().AsReadOnly();
}
