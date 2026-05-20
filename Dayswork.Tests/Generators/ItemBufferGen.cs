using Dayswork.Core.Inventory;
using FsCheck;

namespace Dayswork.Tests.Generators;

public static class ItemBufferGen
{
    // Generates a random non-empty item id.
    public static Gen<string> ItemId() =>
        Arb.Generate<NonEmptyString>().Select(s => s.Get);

    // Generates a random positive quantity.
    public static Gen<int> Quantity() =>
        Gen.Choose(1, 999);

    // Generates a list of (itemId, quantity) pairs.
    public static Gen<IReadOnlyList<(string itemId, int quantity)>> ItemList() =>
        Gen.ListOf(
            ItemId().SelectMany(id => Quantity().Select(qty => (id, qty))))
           .Select(list => (IReadOnlyList<(string, int)>)list.ToList().AsReadOnly());

    // Populates a fresh ItemBuffer from a non-empty generated list and returns both.
    // Uses Where to exclude empty lists so callers can assume buffer is non-empty.
    public static Gen<(ItemBuffer buffer, IReadOnlyList<(string itemId, int quantity)> source)> PopulatedBuffer() =>
        ItemList()
            .Where(items => items.Count > 0)
            .Select(items =>
            {
                var buf = new ItemBuffer();
                foreach (var (id, qty) in items)
                    buf.Add(id, qty);
                return (buf, items);
            });
}
