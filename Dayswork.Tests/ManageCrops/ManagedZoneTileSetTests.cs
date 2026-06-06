using Dayswork.Core.Crops;
using Dayswork.Core.Domain;
using FsCheck;
using FsCheck.Xunit;
using Xunit;

namespace Dayswork.Tests.ManageCrops;

public sealed class ManagedZoneTileSetTests
{
    private static CropZoneAssignment FarmZone(int x0, int y0, int x1, int y1)
    {
        var crop = new CropDescriptor("(O)24", "472", null, 4, null, null, new[] { Season.Spring });
        var choice = new SeasonCropChoice(Season.Spring, crop);
        return new CropZoneAssignment(
            new Zone("Farm", new TileCoord(x0, y0), new TileCoord(x1, y1)),
            CropAssignmentMode.Seasonal,
            new[] { choice });
    }

    [Fact]
    public void TileInsideZone_IsManaged()
    {
        var zones = new[] { FarmZone(2, 2, 5, 5) };
        Assert.True(ManagedZoneTileSet.IsInManagedZone("Farm", new TileCoord(3, 4), zones));
        Assert.True(ManagedZoneTileSet.IsInManagedZone("Farm", new TileCoord(2, 2), zones));  // edge inclusive
        Assert.True(ManagedZoneTileSet.IsInManagedZone("Farm", new TileCoord(5, 5), zones));
    }

    [Fact]
    public void TileOutsideZone_IsNotManaged()
    {
        var zones = new[] { FarmZone(2, 2, 5, 5) };
        Assert.False(ManagedZoneTileSet.IsInManagedZone("Farm", new TileCoord(1, 1), zones));
        Assert.False(ManagedZoneTileSet.IsInManagedZone("Farm", new TileCoord(6, 5), zones));
    }

    [Fact]
    public void DifferentLocation_IsNotManaged()
    {
        var zones = new[] { FarmZone(2, 2, 5, 5) };
        Assert.False(ManagedZoneTileSet.IsInManagedZone("Greenhouse", new TileCoord(3, 3), zones));
    }

    [Fact]
    public void EmptyAssignments_NeverManaged()
    {
        Assert.False(ManagedZoneTileSet.IsInManagedZone("Farm", new TileCoord(0, 0), System.Array.Empty<CropZoneAssignment>()));
    }

    [Property(MaxTest = 300)]
    public Property IsInManagedZone_IffInsideSomeZoneRectangle()
    {
        var zone = FarmZone(2, 2, 5, 5);
        return Prop.ForAll(
            Gen.Choose(-2, 9).ToArbitrary(),
            Gen.Choose(-2, 9).ToArbitrary(),
            (x, y) =>
            {
                var expected = x >= 2 && x <= 5 && y >= 2 && y <= 5;
                var actual = ManagedZoneTileSet.IsInManagedZone("Farm", new TileCoord(x, y), new[] { zone });
                return expected == actual;
            });
    }
}
