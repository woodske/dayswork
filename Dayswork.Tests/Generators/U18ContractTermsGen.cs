namespace Dayswork.Tests.Generators;

using Dayswork.Core.Domain;
using FsCheck;

public static class U18ContractTermsGen
{
    private static readonly string[] AnimalLocationNames =
    {
        "Coop",
        "Big Coop",
        "Deluxe Coop",
        "Barn",
        "Big Barn",
        "Deluxe Barn",
    };

    public static Arbitrary<IReadOnlySet<TaskKind>> EnabledTaskSet() =>
        Gen.SubListOf(Enum.GetValues<TaskKind>())
            .Where(tasks => tasks.Count > 0)
            .Select(tasks => (IReadOnlySet<TaskKind>)tasks.ToHashSet())
            .ToArbitrary();

    public static Arbitrary<ContractScopeSelection> ScopeSelection() =>
        (from outdoorZoneCount in Gen.Choose(0, 3)
         from outdoorZones in Gen.ListOf(outdoorZoneCount, OutdoorZone().Generator)
         from animalBuildingCount in Gen.Choose(0, 3)
         from animalBuildings in Gen.ListOf(animalBuildingCount, AnimalBuildingSelection().Generator)
         from greenhouseSelected in Arb.Generate<bool>()
         select new ContractScopeSelection(
             OutdoorZones: outdoorZones.ToList(),
             AnimalBuildings: animalBuildings.Distinct().ToList(),
             Greenhouse: greenhouseSelected ? new GreenhouseSelection("Greenhouse") : null))
        .ToArbitrary();

    public static Arbitrary<(ContractScopeSelection Left, ContractScopeSelection Right)> EquivalentOutdoorSelections() =>
        (from x in Gen.Choose(0, 30)
         from y in Gen.Choose(0, 30)
         from width in Gen.Choose(0, 6)
         from height in Gen.Choose(0, 6)
         select BuildEquivalentSelections(x, y, width, height))
        .ToArbitrary();

    private static Arbitrary<Zone> OutdoorZone() =>
        (from x1 in Gen.Choose(0, 30)
         from y1 in Gen.Choose(0, 30)
         from x2 in Gen.Choose(0, 30)
         from y2 in Gen.Choose(0, 30)
         let topLeft = new TileCoord(Math.Min(x1, x2), Math.Min(y1, y2))
         let bottomRight = new TileCoord(Math.Max(x1, x2), Math.Max(y1, y2))
         select new Zone("Farm", topLeft, bottomRight))
        .ToArbitrary();

    private static Arbitrary<AnimalBuildingSelection> AnimalBuildingSelection() =>
        (from locationName in Gen.Elements(AnimalLocationNames)
         from tier in Gen.Elements(Enum.GetValues<AnimalBuildingTier>())
         select new AnimalBuildingSelection(locationName, tier))
        .ToArbitrary();

    private static (ContractScopeSelection Left, ContractScopeSelection Right) BuildEquivalentSelections(
        int x,
        int y,
        int width,
        int height)
    {
        var maxX = x + width;
        var maxY = y + height;
        var splitX = x + Math.Max(0, width / 2);
        var overlapStartX = Math.Max(x, splitX - 1);

        var singleZone = new Zone("Farm", new TileCoord(x, y), new TileCoord(maxX, maxY));
        var leftZone = new Zone("Farm", new TileCoord(x, y), new TileCoord(splitX, maxY));
        var rightZone = new Zone("Farm", new TileCoord(overlapStartX, y), new TileCoord(maxX, maxY));

        return (
            new ContractScopeSelection(
                OutdoorZones: new[] { singleZone },
                AnimalBuildings: Array.Empty<AnimalBuildingSelection>(),
                Greenhouse: null),
            new ContractScopeSelection(
                OutdoorZones: new[] { leftZone, rightZone },
                AnimalBuildings: Array.Empty<AnimalBuildingSelection>(),
                Greenhouse: null));
    }
}
