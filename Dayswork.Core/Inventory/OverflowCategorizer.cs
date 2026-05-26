namespace Dayswork.Core.Inventory;

public sealed class OverflowCategorizer
{
    public IReadOnlyList<OverflowCategory> Categorize(IEnumerable<OverflowItem> overflow)
    {
        if (overflow is null) throw new ArgumentNullException(nameof(overflow));

        return overflow
            .Select(item => new OverflowCategory(
                item.Reason,
                item.Stack.Provenance.Family,
                item.Stack.Provenance.ScopeName))
            .Distinct()
            .OrderBy(category => category.Reason)
            .ThenBy(category => category.ScopeFamily)
            .ThenBy(category => category.ScopeName, StringComparer.Ordinal)
            .ToList();
    }
}
