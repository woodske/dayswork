using Dayswork.Core.Domain;

namespace Dayswork.Core.Inventory;

public sealed class ItemBuffer : IItemBuffer
{
    private readonly List<BufferedItem> _items = new();

    public bool IsEmpty => _items.Count == 0;

    public void Add(string itemId, int quantity, TaskKind sourceTask, OutputScopeProvenance? provenance = null)
    {
        if (string.IsNullOrEmpty(itemId)) throw new ArgumentException("itemId must be non-empty.", nameof(itemId));
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity), "quantity must be positive.");
        _items.Add(new BufferedItem(itemId, quantity, sourceTask, provenance ?? OutputScopeProvenance.Unknown));
    }

    public IReadOnlyList<BufferedItem> TakeAll()
    {
        var result = _items.ToList().AsReadOnly();
        _items.Clear();
        return result;
    }

    public IReadOnlyList<BufferedItem> Snapshot() =>
        _items.ToList().AsReadOnly();
}
