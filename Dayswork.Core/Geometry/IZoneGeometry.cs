namespace Dayswork.Core.Geometry;

using Dayswork.Core.Domain;

public interface IZoneGeometry
{
    IReadOnlyList<TileCoord> EnumerateTiles(Zone zone);
    IReadOnlyList<TileCoord> EnumerateReachableTiles(Zone zone, Func<TileCoord, bool> isPassable);
    IReadOnlyList<TileCoord> EnumerateUniqueTiles(IReadOnlyList<Zone> zones, Func<TileCoord, bool> isPassable);
    int CountReachableTiles(Zone zone, Func<TileCoord, bool> isPassable);
    bool Contains(Zone zone, TileCoord tile);
    bool Intersects(Zone a, Zone b);
}
