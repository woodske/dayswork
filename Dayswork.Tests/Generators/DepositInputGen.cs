using Dayswork.Core.Domain;
using Dayswork.Core.Inventory;
using FsCheck;

namespace Dayswork.Tests.Generators;

// PBT-U14-05 shared generator: produces (buffer snapshot, task→destination map) pairs for
// DepositPlanner properties. Composes ZoneGen.ChestRef so chests can be shared across tasks
// (exercising consolidation) and so some tasks are left unassigned (exercising FD-Q2=A → overflow).
public static class DepositInputGen
{
    private static readonly TaskKind[] AllTasks = Enum.GetValues<TaskKind>();

    // Small id pool so identical ids recur and consolidation (summing) actually happens.
    private static readonly string[] ItemIds = { "(O)388", "(O)390", "(O)709", "(O)378", "(O)24" };

    public static Gen<BufferedItem> BufferedItem() =>
        from id in Gen.Elements(ItemIds)
        from qty in Gen.Choose(1, 999)
        from task in Gen.Elements(AllTasks)
        select new BufferedItem(id, qty, task, OutputScopeProvenance.Unknown);

    public static Gen<IReadOnlyList<BufferedItem>> BufferedItems() =>
        Gen.ListOf(BufferedItem()).Select(l => (IReadOnlyList<BufferedItem>)l.ToList());

    // A destination for one task, or null = "absent from the map" (resolves to automatic overflow).
    private static Gen<DestinationKey?> DestinationOrAbsent(IReadOnlyList<ChestRef> chestPool) =>
        from pick in Gen.Choose(0, 6)
        from chestIdx in chestPool.Count == 0 ? Gen.Constant(0) : Gen.Choose(0, chestPool.Count - 1)
        select pick switch
        {
            0      => (DestinationKey?)null,                    // absent ⇒ automatic overflow (FD-Q2=A)
            1      => AutomaticOutputDestination.Instance,      // explicit automatic overflow
            2 or 3 => ShippingBinDestination.Instance,               // shipping bin
            _      => chestPool.Count == 0                            // chest (or bin if no chests)
                          ? ShippingBinDestination.Instance
                          : new ChestDestination(chestPool[chestIdx]),
        };

    public static Gen<IReadOnlyDictionary<TaskKind, DestinationKey>> Assignments() =>
        from chestCount in Gen.Choose(0, 3)
        from chests in Gen.ListOf(chestCount, ZoneGen.ChestRef().Generator)
        let chestPool = (IReadOnlyList<ChestRef>)chests.Distinct().ToList()
        from destOpts in Gen.ListOf(AllTasks.Length, DestinationOrAbsent(chestPool))
        select BuildMap(destOpts.ToList());

    private static IReadOnlyDictionary<TaskKind, DestinationKey> BuildMap(IReadOnlyList<DestinationKey?> opts)
    {
        var map = new Dictionary<TaskKind, DestinationKey>();
        for (int i = 0; i < AllTasks.Length && i < opts.Count; i++)
            if (opts[i] is not null)
                map[AllTasks[i]] = opts[i]!;
        return map;
    }

    public static Arbitrary<(IReadOnlyList<BufferedItem> snapshot,
                             IReadOnlyDictionary<TaskKind, DestinationKey> assignments)> PlannerInput() =>
        (from items in BufferedItems()
         from assignments in Assignments()
         select (items, assignments)).ToArbitrary();
}
