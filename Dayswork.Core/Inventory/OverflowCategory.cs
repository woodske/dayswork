using Dayswork.Core.Domain;

namespace Dayswork.Core.Inventory;

public sealed record OverflowCategory(
    OverflowReason Reason,
    OutputScopeFamily ScopeFamily,
    string ScopeName);
