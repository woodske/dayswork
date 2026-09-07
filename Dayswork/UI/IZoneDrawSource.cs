using Dayswork.Core.Domain;
using Microsoft.Xna.Framework;

namespace Dayswork.UI;

// Data contract read by ZoneDrawOverlay during RenderedWorld rendering.
// Implemented by ZoneDrawMenu (the on-farm zone-drawing session menu).
internal interface IZoneDrawSource
{
    IReadOnlyList<Zone>           CompletedZones    { get; }
    IReadOnlyList<Zone>           ProtectedZones    { get; }

    // Other farmhands' (other contracts') managed-crop zones — an informational light-purple wash so
    // the player can see where another worker already grows crops. Unlike ProtectedZones, these stay
    // selectable (the per-day WorkClaimRegistry arbitrates any tile two farmhands both claim). Empty
    // outside the managed-crop draw layer.
    IReadOnlyList<Zone>           OtherWorkerZones  { get; }

    IReadOnlyList<BuildingOutline> SelectedBuildings { get; }
    bool       IsInZoneDrawMode { get; }
    TileCoord? DragStart        { get; }
    TileCoord  DragCurrent      { get; }

    // Fill color for completed-zone highlights — lets each draw layer (general tasks vs managed
    // crops) render in its own color so they are visually distinct.
    Color ZoneFillColor { get; }
    Color ProtectedZoneFillColor { get; }

    // Managed-crop layer: highlight only the individual valid (tillable/plantable, non-sprinkler)
    // tiles inside a zone/drag rather than the whole rectangle. False for general task scope, which
    // fills the full rectangle.
    bool HighlightValidTilesOnly { get; }
    bool IsHighlightableTile(TileCoord tile);
}
