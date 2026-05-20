namespace Dayswork.Core.Inventory;

public interface IItemBuffer
{
    bool IsEmpty { get; }
    void Add(string itemId, int quantity);
    IReadOnlyList<(string itemId, int quantity)> TakeAll();
    IReadOnlyList<(string itemId, int quantity)> Snapshot();
}
