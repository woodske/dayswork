using Dayswork.Core.Domain;
using Dayswork.Core.Inventory;
using FsCheck;

namespace Dayswork.Tests.Inventory;

public static class OverflowGenerators
{
    private static readonly string[] ItemIds = { "(O)388", "(O)390", "(O)430", "(O)709" };
    private static readonly OverflowReason[] Reasons = Enum.GetValues<OverflowReason>();
    private static readonly OutputScopeFamily[] Families = Enum.GetValues<OutputScopeFamily>();

    public static Arbitrary<IReadOnlyList<OverflowItem>> OverflowItems()
    {
        var gen =
            from count in Gen.Choose(0, 12)
            from items in Gen.ListOf(count,
                from itemId in Gen.Elements(ItemIds)
                from quantity in Gen.Choose(1, 9)
                from task in Gen.Elements(Enum.GetValues<TaskKind>())
                from reason in Gen.Elements(Reasons)
                from family in Gen.Elements(Families)
                from scopeName in Gen.Elements("", "Farm", "Barn", "Big Coop", "Greenhouse")
                let provenance = new OutputScopeProvenance(family, scopeName)
                select new OverflowItem(
                    new RoutedItemStack(itemId, quantity, task, provenance),
                    reason))
            select (IReadOnlyList<OverflowItem>)items.ToList();

        return Arb.From(gen);
    }
}
