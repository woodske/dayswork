using Dayswork.Core.Domain;

namespace Dayswork.UI;

internal static class ZoneOverlapPolicy
{
    public static bool OverlapsAny(IEnumerable<Zone> zones, TileCoord topLeft, TileCoord bottomRight) =>
        zones.Any(zone => ZonesOverlap(zone, topLeft, bottomRight));

    public static bool ZonesOverlap(Zone zone, TileCoord topLeft, TileCoord bottomRight) =>
        zone.TopLeft.X <= bottomRight.X && zone.BottomRight.X >= topLeft.X
        && zone.TopLeft.Y <= bottomRight.Y && zone.BottomRight.Y >= topLeft.Y;
}
