namespace Dayswork.Tests.Geometry;

using Dayswork.Core.Domain;
using Dayswork.Core.Geometry;
using Dayswork.Tests.Generators;
using FsCheck;
using FsCheck.Xunit;
using Newtonsoft.Json;
using Xunit;

public class ZoneGeometryTests
{
    private readonly ZoneGeometry _geo = new();

    // ── EnumerateTiles determinism ────────────────────────────────────────────

    [Fact]
    public void EnumerateTiles_ReturnsExactTileCount()
    {
        var zone = new Zone("Farm", new TileCoord(0, 0), new TileCoord(2, 2));
        Assert.Equal(9, _geo.EnumerateTiles(zone).Count);
    }

    [Fact]
    public void EnumerateTiles_RowMajorOrder()
    {
        var zone = new Zone("Farm", new TileCoord(0, 0), new TileCoord(1, 1));
        var expected = new[]
        {
            new TileCoord(0, 0), new TileCoord(1, 0),
            new TileCoord(0, 1), new TileCoord(1, 1),
        };
        Assert.Equal(expected, _geo.EnumerateTiles(zone));
    }

    [Fact]
    public void EnumerateTiles_SingleTileZone()
    {
        var zone = new Zone("Farm", new TileCoord(5, 8), new TileCoord(5, 8));
        var tiles = _geo.EnumerateTiles(zone);
        Assert.Single(tiles);
        Assert.Equal(new TileCoord(5, 8), tiles[0]);
    }

    // ── Contains ─────────────────────────────────────────────────────────────

    [Fact]
    public void Contains_ReturnsTrueForCornerTiles()
    {
        var zone = new Zone("Farm", new TileCoord(2, 3), new TileCoord(5, 6));
        Assert.True(_geo.Contains(zone, new TileCoord(2, 3)));
        Assert.True(_geo.Contains(zone, new TileCoord(5, 6)));
        Assert.True(_geo.Contains(zone, new TileCoord(3, 4)));
    }

    [Fact]
    public void Contains_ReturnsFalseForOutsideTile()
    {
        var zone = new Zone("Farm", new TileCoord(2, 3), new TileCoord(5, 6));
        Assert.False(_geo.Contains(zone, new TileCoord(1, 3)));
        Assert.False(_geo.Contains(zone, new TileCoord(2, 2)));
        Assert.False(_geo.Contains(zone, new TileCoord(6, 6)));
    }

    // ── Intersects ───────────────────────────────────────────────────────────

    [Fact]
    public void Intersects_ReturnsTrue_WhenOverlapping()
    {
        var a = new Zone("Farm", new TileCoord(0, 0), new TileCoord(3, 3));
        var b = new Zone("Farm", new TileCoord(2, 2), new TileCoord(5, 5));
        Assert.True(_geo.Intersects(a, b));
    }

    [Fact]
    public void Intersects_ReturnsFalse_WhenAdjacent()
    {
        var a = new Zone("Farm", new TileCoord(0, 0), new TileCoord(2, 2));
        var b = new Zone("Farm", new TileCoord(3, 0), new TileCoord(5, 2));
        Assert.False(_geo.Intersects(a, b));
    }

    [Fact]
    public void Intersects_ReturnsTrue_WhenOneContainsOther()
    {
        var outer = new Zone("Farm", new TileCoord(0, 0), new TileCoord(10, 10));
        var inner = new Zone("Farm", new TileCoord(3, 3), new TileCoord(5, 5));
        Assert.True(_geo.Intersects(outer, inner));
    }

    // ── DestinationKey equality ───────────────────────────────────────────────

    [Fact]
    public void ChestDestination_EqualityByValue()
    {
        var ref1 = new ChestRef("Farm", new TileCoord(5, 8));
        var ref2 = new ChestRef("Farm", new TileCoord(5, 8));
        Assert.Equal(new ChestDestination(ref1), new ChestDestination(ref2));
    }

    [Fact]
    public void ChestDestination_InequalityOnDifferentTile()
    {
        var dest1 = new ChestDestination(new ChestRef("Farm", new TileCoord(5, 8)));
        var dest2 = new ChestDestination(new ChestRef("Farm", new TileCoord(5, 9)));
        Assert.NotEqual(dest1, dest2);
    }

    [Fact]
    public void ShippingBinDestination_InstanceEquality()
    {
        Assert.Equal(ShippingBinDestination.Instance, new ShippingBinDestination());
    }

    [Fact]
    public void AutomaticOutputDestination_DifferentFromShippingBin()
    {
        Assert.NotEqual<DestinationKey>(AutomaticOutputDestination.Instance, ShippingBinDestination.Instance);
    }

    // ── Zone JSON round-trip ─────────────────────────────────────────

    [Property(MaxTest = 1000)]
    public Property Zone_JsonRoundTrip() =>
        Prop.ForAll(ZoneGen.Zone(), zone =>
        {
            var json = JsonConvert.SerializeObject(zone);
            var deserialized = JsonConvert.DeserializeObject<Zone>(json);
            return zone.Equals(deserialized);
        });

    // ── EnumerateUniqueTiles invariants ──────────────────────────────

    [Property(MaxTest = 1000)]
    public Property EnumerateUniqueTiles_Commutativity() =>
        Prop.ForAll(ZoneGen.Zone(), ZoneGen.Zone(), (a, b) =>
        {
            var ab = _geo.EnumerateUniqueTiles(new[] { a, b }, _ => true).ToHashSet();
            var ba = _geo.EnumerateUniqueTiles(new[] { b, a }, _ => true).ToHashSet();
            return ab.SetEquals(ba);
        });

    [Property(MaxTest = 1000)]
    public Property EnumerateUniqueTiles_Idempotency() =>
        Prop.ForAll(ZoneGen.Zone(), a =>
        {
            var single = _geo.EnumerateUniqueTiles(new[] { a }, _ => true).Count;
            var doubled = _geo.EnumerateUniqueTiles(new[] { a, a }, _ => true).Count;
            return single == doubled;
        });

    [Property(MaxTest = 1000)]
    public Property EnumerateUniqueTiles_AreaConservation() =>
        Prop.ForAll(ZoneGen.Zone(), ZoneGen.Zone(), (a, b) =>
        {
            var union = _geo.EnumerateUniqueTiles(new[] { a, b }, _ => true).Count;
            var sumIndividual = _geo.CountReachableTiles(a, _ => true) + _geo.CountReachableTiles(b, _ => true);
            return union <= sumIndividual;
        });
}
