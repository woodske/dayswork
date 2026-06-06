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

    [Fact]
    public void OverlapsAny_ReturnsFalseWhenNoProtectedZonesExist()
    {
        // First/only crop group: nothing is protected, so the player may draw freely (BR-MC04: empty protected set).
        var overlaps = ZoneOverlapPolicy.OverlapsAny(
            Array.Empty<Zone>(),
            new TileCoord(0, 0),
            new TileCoord(4, 4));

        Assert.False(overlaps);
    }

    [Fact]
    public void OverlapsAny_ReturnsTrueWhenIntersectingAnyOfSeveralProtectedZones()
    {
        // Multiple other crop groups are protected at once; overlapping the second one still rejects (FR-MC-06 "any").
        var protectedZones = new[]
        {
            new Zone("Farm", new TileCoord(0, 0), new TileCoord(2, 2)),
            new Zone("Farm", new TileCoord(10, 10), new TileCoord(12, 12)),
        };

        var overlaps = ZoneOverlapPolicy.OverlapsAny(
            protectedZones,
            new TileCoord(11, 11),
            new TileCoord(15, 15));

        Assert.True(overlaps);
    }
}
