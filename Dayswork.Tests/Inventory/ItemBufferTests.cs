using Dayswork.Core.Domain;
using Dayswork.Core.Inventory;
using Dayswork.Tests.Generators;
using FsCheck;
using FsCheck.Xunit;

namespace Dayswork.Tests.Inventory;

public sealed class ItemBufferTests
{
    // Snapshot is non-destructive — same items as subsequent TakeAll, buffer still has items after.
    [Property(MaxTest = 500, Replay = "")]
    public Property Snapshot_Is_NonDestructive()
    {
        return Prop.ForAll(ItemBufferGen.PopulatedBuffer().ToArbitrary(), tuple =>
        {
            var (buffer, _) = tuple;
            var snapshot = buffer.Snapshot();
            var taken    = buffer.TakeAll();

            var sameItems  = snapshot.SequenceEqual(taken);
            var wasNonEmpty = snapshot.Count > 0;

            return (sameItems && wasNonEmpty)
                .Label($"snapshot.Count={snapshot.Count} taken.Count={taken.Count} match={sameItems}");
        });
    }

    // TakeAll preserves total quantity — sum of all added quantities equals sum of taken quantities.
    [Property(MaxTest = 500, Replay = "")]
    public Property TakeAll_Preserves_Total_Quantity()
    {
        return Prop.ForAll(ItemBufferGen.ItemList().ToArbitrary(), items =>
        {
            var buffer = new ItemBuffer();
            foreach (var (id, qty) in items)
                buffer.Add(id, qty, TaskKind.ClearWeeds);

            var taken        = buffer.TakeAll();
            var addedTotal   = items.Sum(x => x.quantity);
            var takenTotal   = taken.Sum(x => x.Quantity);

            return (addedTotal == takenTotal)
                .Label($"added={addedTotal} taken={takenTotal}");
        });
    }
}
