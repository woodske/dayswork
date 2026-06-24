using Dayswork.Core.Domain;

namespace Dayswork.Core.Inventory;

// A grouped output stack that still preserves the originating task and scope provenance.
// Used during deposit planning and overflow handling so routing stays task-owned while
// explanation/categorization can still reason about where the output came from.
// FlavorId: see BufferedItem — an opaque token for a captured flavored/colored item, carried so the
// deposit layer can clone the real item back instead of reconstructing a generic one from the id.
public sealed record RoutedItemStack(
    string QualifiedItemId,
    int Quantity,
    TaskKind SourceTask,
    OutputScopeProvenance Provenance,
    int Quality = 0,
    string? FlavorId = null);
