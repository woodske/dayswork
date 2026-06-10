namespace Dayswork.Core.Crops;

using Dayswork.Core.Domain;

/// <summary>
/// Pure helper that sizes the shift's seed/fertilizer purchase from the
/// tiles the farmhand <em>plans</em> to plant this shift — independent of how many happen to be
/// tilled by the time it shops. A fresh untilled zone has no tilled-empty tiles yet, so the
/// purchase must be sized from all non-cropped tiles the worker intends to till and plant
/// (plus harvest-and-replant tiles when auto-replant is on), gated by planting viability.
/// </summary>
public static class PlannedPlantableTileCounter
{
    public static int CountPlantable(
        FieldState field,
        CropZoneAssignment assignment,
        SeasonCropChoice choice,
        bool isViable)
    {
        if (!isViable)
            return 0;

        // A non-regrow crop's ready tile is harvested first and then replanted the same shift
        // when auto-replant is on, so it is a planned plantable tile too.
        var willReplant = choice.AutoReplant && choice.Crop.RegrowDays is null;

        var count = 0;
        foreach (var tile in field.Tiles)
        {
            if (!Contains(assignment.Zone, tile.Tile))
                continue;

            if (!tile.HasCrop)
                count++; // tilled-empty, bare, or debris (debris is cleared then tilled this shift)
            else if (tile.ReadyToHarvest && willReplant)
                count++;
        }

        return count;
    }

    private static bool Contains(Zone zone, TileCoord tile) =>
        tile.X >= zone.TopLeft.X
        && tile.X <= zone.BottomRight.X
        && tile.Y >= zone.TopLeft.Y
        && tile.Y <= zone.BottomRight.Y;
}
