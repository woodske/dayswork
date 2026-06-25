using Dayswork.Core.Domain;

namespace Dayswork.Core.Inventory;

// A single collected drop, tagged with the task that produced it.
// SourceTask lets the DepositPlanner resolve this item's destination via the contract's
// TaskDestinations map at deposit time, keeping the buffer a dumb, pure record.
//
// FlavorId is an opaque token (null for plain items) identifying a captured flavored/colored item
// — roe, wine, jelly, flavored honey, etc. — whose identity (PreserveId/PreserveType/color) and
// price cannot be reconstructed from QualifiedItemId alone. The game-touching layer registers the
// real Item under this token at collect time and clones it back at deposit time. Two drops sharing
// a FlavorId are the same flavor and consolidate; different flavors stay distinct.
public sealed record BufferedItem(
    string QualifiedItemId,
    int Quantity,
    TaskKind SourceTask,
    OutputScopeProvenance Provenance,
    int Quality = 0,
    string? FlavorId = null);
