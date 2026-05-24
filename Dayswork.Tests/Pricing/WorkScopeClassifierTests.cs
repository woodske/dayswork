namespace Dayswork.Tests.Pricing;

using Dayswork.Core.Domain;
using Dayswork.Core.Pricing;
using Xunit;

public sealed class WorkScopeClassifierTests
{
    [Fact]
    public void Classify_OverlappingOutdoorZones_DoNotDoubleCountTiles()
    {
        var selection = new ContractScopeSelection(
            OutdoorZones: new[]
            {
                new Zone("Farm", new TileCoord(0, 0), new TileCoord(1, 1)),
                new Zone("Farm", new TileCoord(1, 0), new TileCoord(2, 1)),
            },
            AnimalBuildings: Array.Empty<AnimalBuildingSelection>(),
            Greenhouse: null);

        var classifier = new WorkScopeClassifier();
        var scopes = classifier.Classify(
            selection,
            new HashSet<TaskKind> { TaskKind.HarvestCrops });

        Assert.NotNull(scopes.OutdoorWork);
        Assert.Equal(6, scopes.OutdoorWork!.TotalTileCount);
        Assert.Equal(6, scopes.OutdoorWork.NormalizedZones.Count);
    }

    [Fact]
    public void Classify_OmitsIrrelevantScopeFamilies()
    {
        var selection = new ContractScopeSelection(
            OutdoorZones: new[] { new Zone("Farm", new TileCoord(0, 0), new TileCoord(0, 0)) },
            AnimalBuildings: new[] { new AnimalBuildingSelection("Coop", AnimalBuildingTier.Coop) },
            Greenhouse: new GreenhouseSelection("Greenhouse"));

        var classifier = new WorkScopeClassifier();
        var scopes = classifier.Classify(
            selection,
            new HashSet<TaskKind> { TaskKind.PetAnimals });

        Assert.Null(scopes.OutdoorWork);
        Assert.Null(scopes.GreenhouseWork);
        Assert.Single(scopes.AnimalBuildings);
    }
}
