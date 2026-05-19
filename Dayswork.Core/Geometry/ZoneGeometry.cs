namespace Dayswork.Core.Geometry;

using Dayswork.Core.Domain;

public sealed class ZoneGeometry : IZoneGeometry
{
    public IReadOnlyList<TileCoord> EnumerateTiles(Zone zone)
    {
        var width = zone.BottomRight.X - zone.TopLeft.X + 1;
        var height = zone.BottomRight.Y - zone.TopLeft.Y + 1;
        var result = new List<TileCoord>(width * height);
        for (var y = zone.TopLeft.Y; y <= zone.BottomRight.Y; y++)
        for (var x = zone.TopLeft.X; x <= zone.BottomRight.X; x++)
            result.Add(new TileCoord(x, y));
        return result;
    }

    public IReadOnlyList<TileCoord> EnumerateReachableTiles(Zone zone, Func<TileCoord, bool> isPassable)
    {
        var result = new List<TileCoord>();
        for (var y = zone.TopLeft.Y; y <= zone.BottomRight.Y; y++)
        for (var x = zone.TopLeft.X; x <= zone.BottomRight.X; x++)
        {
            var tile = new TileCoord(x, y);
            if (isPassable(tile))
                result.Add(tile);
        }
        return result;
    }

    public IReadOnlyList<TileCoord> EnumerateUniqueTiles(IReadOnlyList<Zone> zones, Func<TileCoord, bool> isPassable)
    {
        var seen = new HashSet<TileCoord>();
        var result = new List<TileCoord>();
        foreach (var zone in zones)
        for (var y = zone.TopLeft.Y; y <= zone.BottomRight.Y; y++)
        for (var x = zone.TopLeft.X; x <= zone.BottomRight.X; x++)
        {
            var tile = new TileCoord(x, y);
            if (isPassable(tile) && seen.Add(tile))
                result.Add(tile);
        }
        return result;
    }

    public int CountReachableTiles(Zone zone, Func<TileCoord, bool> isPassable)
    {
        var count = 0;
        for (var y = zone.TopLeft.Y; y <= zone.BottomRight.Y; y++)
        for (var x = zone.TopLeft.X; x <= zone.BottomRight.X; x++)
            if (isPassable(new TileCoord(x, y)))
                count++;
        return count;
    }

    public bool Contains(Zone zone, TileCoord tile) =>
        tile.X >= zone.TopLeft.X && tile.X <= zone.BottomRight.X &&
        tile.Y >= zone.TopLeft.Y && tile.Y <= zone.BottomRight.Y;

    public bool Intersects(Zone a, Zone b) =>
        a.TopLeft.X <= b.BottomRight.X && b.TopLeft.X <= a.BottomRight.X &&
        a.TopLeft.Y <= b.BottomRight.Y && b.TopLeft.Y <= a.BottomRight.Y;
}
