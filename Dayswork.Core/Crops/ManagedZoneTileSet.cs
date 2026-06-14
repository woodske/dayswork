namespace Dayswork.Core.Crops;

using Dayswork.Core.Domain;

/// <summary>
/// Pure coexistence helper: a tile is serviced by the managed-crop path —
/// and therefore excluded from the general WaterCrops/HarvestCrops scans — exactly when it lies
/// inside one of the contract's managed crop zones for that location.
/// </summary>
public static class ManagedZoneTileSet
{
    public static bool IsInManagedZone(
        string locationName,
        TileCoord tile,
        IReadOnlyList<CropZoneAssignment> assignments)
    {
        if (assignments is null || assignments.Count == 0)
            return false;

        foreach (var assignment in assignments)
        {
            var zone = assignment.Zone;
            if (!string.Equals(zone.LocationName, locationName, StringComparison.Ordinal))
                continue;

            if (tile.X >= zone.TopLeft.X
                && tile.X <= zone.BottomRight.X
                && tile.Y >= zone.TopLeft.Y
                && tile.Y <= zone.BottomRight.Y)
                return true;
        }

        return false;
    }
}
