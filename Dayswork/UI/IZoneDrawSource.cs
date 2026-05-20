using Dayswork.Core.Domain;

namespace Dayswork.UI;

// Data contract read by ZoneDrawOverlay during RenderedWorld rendering.
// Implemented by ZoneDrawMenu (the on-farm zone-drawing session menu).
internal interface IZoneDrawSource
{
    IReadOnlyList<Zone>           CompletedZones    { get; }
    IReadOnlyList<BuildingOutline> SelectedBuildings { get; }
    bool       IsInZoneDrawMode { get; }
    TileCoord? DragStart        { get; }
    TileCoord  DragCurrent      { get; }
}
