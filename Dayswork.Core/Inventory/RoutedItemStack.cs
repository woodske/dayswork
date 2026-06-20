using Dayswork.Core.Domain;

namespace Dayswork.Core.Inventory;

// A grouped output stack that still preserves the originating task and scope provenance.
// Used during deposit planning and overflow handling so routing stays task-owned while
// explanation/categorization can still reason about where the output came from.
public sealed record RoutedItemStack(
    string QualifiedItemId,
    int Quantity,
    TaskKind SourceTask,
    OutputScopeProvenance Provenance,
    int Quality = 0);
