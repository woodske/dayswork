using Dayswork.Core.Domain;

namespace Dayswork.Core.Inventory;

// A single collected drop, tagged with the task that produced it (Pattern L / FD-Q1=A).
// SourceTask lets the DepositPlanner resolve this item's destination via the contract's
// TaskDestinations map at deposit time, keeping the buffer a dumb, pure record.
public sealed record BufferedItem(string QualifiedItemId, int Quantity, TaskKind SourceTask);
