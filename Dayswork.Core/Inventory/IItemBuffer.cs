using Dayswork.Core.Domain;

namespace Dayswork.Core.Inventory;

public interface IItemBuffer
{
    bool IsEmpty { get; }
    void Add(string itemId, int quantity, TaskKind sourceTask);
    IReadOnlyList<BufferedItem> TakeAll();
    IReadOnlyList<BufferedItem> Snapshot();
}
