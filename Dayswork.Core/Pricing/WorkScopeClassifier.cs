namespace Dayswork.Core.Pricing;

using Dayswork.Core.Crops;
using Dayswork.Core.Domain;

public sealed class WorkScopeClassifier
{
    public WorkScopeSet Classify(ContractScopeSelection selection, IReadOnlySet<TaskKind> enabledTasks, CropPlan? cropPlan = null)
    {
        OutdoorWorkScope? outdoorWork = null;
        if (selection.OutdoorZones.Count > 0 && enabledTasks.Any(TaskKindSets.IsOutdoorService))
            outdoorWork = BuildOutdoorWorkScope(selection.OutdoorZones);

        var animalScopes = new List<AnimalBuildingScope>();
        if (enabledTasks.Any(TaskKindSets.IsAnimalService))
        {
            animalScopes.AddRange(
                selection.AnimalBuildings
                    .Where(building => !string.IsNullOrWhiteSpace(building.LocationName))
                    .Distinct()
                    .OrderBy(building => building.LocationName, StringComparer.Ordinal)
                    .ThenBy(building => building.Tier)
                    .Select(building => new AnimalBuildingScope(building.LocationName, building.Tier)));
        }

        var greenhouseWorks = new List<GreenhouseWorkScope>();
        if (enabledTasks.Any(TaskKindSets.IsGreenhouseService))
        {
            greenhouseWorks.AddRange(
                selection.Greenhouses
                    .Where(greenhouse => !string.IsNullOrWhiteSpace(greenhouse.LocationName))
                    .Select(greenhouse => greenhouse.LocationName)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(locationName => locationName, StringComparer.Ordinal)
                    .Select(locationName => new GreenhouseWorkScope(locationName)));
        }

        var managedCrops = cropPlan is { IsEnabled: true }
            ? new ManagedCropWorkScope(cropPlan.Assignments)
            : null;

        return new WorkScopeSet(outdoorWork, animalScopes, greenhouseWorks, managedCrops);
    }

    private static OutdoorWorkScope BuildOutdoorWorkScope(IReadOnlyList<Zone> zones)
    {
        var uniqueTiles = new HashSet<(string LocationName, int X, int Y)>();
        foreach (var zone in zones)
        {
            for (var y = zone.TopLeft.Y; y <= zone.BottomRight.Y; y++)
            for (var x = zone.TopLeft.X; x <= zone.BottomRight.X; x++)
                uniqueTiles.Add((zone.LocationName, x, y));
        }

        var normalizedZones = uniqueTiles
            .OrderBy(tile => tile.LocationName, StringComparer.Ordinal)
            .ThenBy(tile => tile.Y)
            .ThenBy(tile => tile.X)
            .Select(tile => new Zone(
                tile.LocationName,
                new TileCoord(tile.X, tile.Y),
                new TileCoord(tile.X, tile.Y)))
            .ToList();

        return new OutdoorWorkScope(normalizedZones, normalizedZones.Count);
    }
}
