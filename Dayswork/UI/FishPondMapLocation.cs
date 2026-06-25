using Dayswork.Core.Domain;

namespace Dayswork.UI;

/// <summary>
/// One pond's footprint on the selection map: its anchor (top-left <c>tileX</c>/<c>tileY</c>, the
/// identity used by <see cref="Dayswork.Core.FishPonds.FishPondRef"/>) and footprint size, so a click
/// anywhere on the pond resolves to the same anchor. UI-only.
/// </summary>
internal sealed record FishPondFootprint(TileCoord Anchor, int Width, int Height)
{
    public bool Contains(TileCoord tile) =>
        tile.X >= Anchor.X && tile.X < Anchor.X + Width &&
        tile.Y >= Anchor.Y && tile.Y < Anchor.Y + Height;

    /// <summary>The bounding-box zone covering the whole footprint (for overlay highlighting).</summary>
    public Zone ToZone(string locationName) =>
        new(locationName, Anchor, new TileCoord(Anchor.X + Width - 1, Anchor.Y + Height - 1));
}

/// <summary>
/// One location the fish-pond selection map session can display: its name, a friendly label for the
/// location switcher, and the ponds present there. UI-only.
/// </summary>
internal sealed record FishPondMapLocation(
    string LocationName,
    string DisplayName,
    IReadOnlyList<FishPondFootprint> Ponds);
