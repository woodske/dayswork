using Dayswork.Core.Domain;

namespace Dayswork.Core.Shifts;

public sealed class ShiftPlanBuilder
{
    public IReadOnlyList<WorkBatch> BuildBatchPlan(
        IReadOnlyList<Zone> zones,
        IReadOnlySet<TaskKind> enabledTasks,
        Func<string, bool> isAnimalHouse)
    {
        if (zones is null) throw new ArgumentNullException(nameof(zones));
        if (enabledTasks is null) throw new ArgumentNullException(nameof(enabledTasks));
        if (isAnimalHouse is null) throw new ArgumentNullException(nameof(isAnimalHouse));

        var byLocation = new Dictionary<string, BatchKind>(StringComparer.Ordinal);
        var locationOrder = new List<string>();

        foreach (var zone in zones)
        {
            var locationName = string.IsNullOrWhiteSpace(zone.LocationName)
                ? "Farm"
                : zone.LocationName;

            if (!byLocation.ContainsKey(locationName))
                locationOrder.Add(locationName);

            if (locationName == "Farm")
            {
                byLocation[locationName] = BatchKind.OutdoorFarm;
                continue;
            }

            byLocation[locationName] = isAnimalHouse(locationName)
                ? BatchKind.AnimalBuilding
                : BatchKind.Interior;
        }

        var batches = locationOrder
            .Select(location => CreateSkeleton(location, byLocation[location], enabledTasks))
            .OrderBy(batch => batch.Kind)
            .ThenBy(batch => locationOrder.IndexOf(batch.LocationName))
            .ToList();

        return batches;
    }

    private static WorkBatch CreateSkeleton(
        string locationName,
        BatchKind kind,
        IReadOnlySet<TaskKind> enabledTasks) =>
        new(
            locationName,
            kind,
            Array.Empty<WorkItem>(),
            Array.Empty<AnimalWorkItem>(),
            kind == BatchKind.AnimalBuilding && enabledTasks.Contains(TaskKind.FeedAnimals));
}
