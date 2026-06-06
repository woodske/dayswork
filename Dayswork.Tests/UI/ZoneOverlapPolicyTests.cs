namespace Dayswork.Tests.UI;

using Dayswork.Core.Domain;
using Dayswork.UI;
using Xunit;

public sealed class ZoneOverlapPolicyTests
{
    [Fact]
    public void OverlapsAny_ReturnsTrueForProtectedTileIntersection()
    {
        var protectedZones = new[]
        {
            new Zone("Farm", new TileCoord(3, 3), new TileCoord(5, 5)),
        };

        var overlaps = ZoneOverlapPolicy.OverlapsAny(
            protectedZones,
            new TileCoord(4, 4),
            new TileCoord(7, 7));

        Assert.True(overlaps);
    }

    [Fact]
    public void OverlapsAny_ReturnsFalseWhenNewGroupTilesDoNotTouchProtectedZones()
    {
        var protectedZones = new[]
        {
            new Zone("Farm", new TileCoord(3, 3), new TileCoord(5, 5)),
        };

        var overlaps = ZoneOverlapPolicy.OverlapsAny(
            protectedZones,
            new TileCoord(6, 6),
            new TileCoord(7, 7));

        Assert.False(overlaps);
    }

    [Fact]
    public void ZonesOverlap_TreatsSharedEdgeTileAsOverlap()
    {
        var existing = new Zone("Farm", new TileCoord(3, 3), new TileCoord(5, 5));

        var overlaps = ZoneOverlapPolicy.ZonesOverlap(
            existing,
            new TileCoord(5, 5),
            new TileCoord(7, 7));

        Assert.True(overlaps);
    }
}
