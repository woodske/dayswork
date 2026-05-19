namespace Dayswork.Tests.Generators;

using Dayswork.Core.Domain;
using FsCheck;

public static class ZoneGen
{
    private static readonly string[] LocationNames = { "Farm", "Greenhouse", "Barn", "Coop" };

    public static Arbitrary<TileCoord> TileCoord() =>
        (from x in Gen.Choose(-5, 200)
         from y in Gen.Choose(-5, 200)
         select new TileCoord(x, y)).ToArbitrary();

    public static Arbitrary<Zone> Zone() =>
        (from x1 in Gen.Choose(-5, 200)
         from y1 in Gen.Choose(-5, 200)
         from x2 in Gen.Choose(-5, 200)
         from y2 in Gen.Choose(-5, 200)
         from loc in Gen.Elements(LocationNames)
         let topLeft = new TileCoord(Math.Min(x1, x2), Math.Min(y1, y2))
         let bottomRight = new TileCoord(Math.Max(x1, x2), Math.Max(y1, y2))
         select new Zone(loc, topLeft, bottomRight)).ToArbitrary();

    public static Arbitrary<ChestRef> ChestRef() =>
        (from loc in Gen.Elements(LocationNames)
         from tile in TileCoord().Generator
         select new ChestRef(loc, tile)).ToArbitrary();

    public static Arbitrary<IReadOnlyList<Zone>> ZoneList() =>
        (from n in Gen.Choose(1, 5)
         from zones in Gen.ListOf(n, Zone().Generator)
         select (IReadOnlyList<Zone>)zones.ToList()).ToArbitrary();
}
