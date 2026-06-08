using Dayswork.Core.Crops;
using Dayswork.Core.Domain;

namespace Dayswork.Core.Shifts;

public sealed class ShiftPlanBuilder
{
    public IReadOnlyList<WorkBatch> BuildBatchPlan(
        WorkScopeSet scopes,
        IReadOnlySet<TaskKind> enabledTasks)
    {
        if (scopes is null) throw new ArgumentNullException(nameof(scopes));
        if (enabledTasks is null) throw new ArgumentNullException(nameof(enabledTasks));

        var batches = new List<WorkBatch>();

        var animalTasks = Order(enabledTasks.Where(TaskKindSets.IsAnimalService));
        var outdoorAnimalTasks = animalTasks
            .Where(task => task != TaskKind.FeedAnimals)
            .ToList();
        if (animalTasks.Count > 0)
        {
            // Per-building grouping (TODO-09): emit each building's interior batch immediately
            // followed by that building's own grazing pass, so the worker fully services one
            // building (indoors + its grazing animals) before moving to the next, instead of doing
            // every interior first and then a single combined outdoor pass. Farm-wide forage
            // (truffles) is not building-owned, so it runs once at the end as a FarmForage pass.
            foreach (var building in scopes.AnimalBuildings
                         .OrderBy(scope => scope.LocationName, StringComparer.Ordinal)
                         .ThenBy(scope => scope.Tier))
            {
                batches.Add(CreateSkeleton(
                    building.LocationName,
                    BatchKind.AnimalBuilding,
                    animalTasks,
                    feedBuilding: animalTasks.Contains(TaskKind.FeedAnimals)));

                if (outdoorAnimalTasks.Count > 0)
                    batches.Add(CreateSkeleton(
                        building.LocationName,
                        BatchKind.OutdoorAnimals,
                        outdoorAnimalTasks,
                        feedBuilding: false));
            }

            if (outdoorAnimalTasks.Contains(TaskKind.CollectAnimalProducts))
                batches.Add(CreateSkeleton(
                    "Farm",
                    BatchKind.FarmForage,
                    new[] { TaskKind.CollectAnimalProducts },
                    feedBuilding: false));
        }

        var managedCropLocations = ManagedCropLocations(scopes.ManagedCrops);
        EmitManagedCropBatches(
            batches,
            managedCropLocations.Where(location => !string.Equals(location, "Farm", StringComparison.Ordinal)));

        var greenhouseTasks = Order(enabledTasks.Where(TaskKindSets.IsGreenhouseService));
        if (greenhouseTasks.Count > 0)
        {
            // One greenhouse batch per selected greenhouse (TODO-10): a farm may expose the vanilla
            // greenhouse and an expansion greenhouse (e.g. SVE's Grandpa's Shed) at once; each is
            // serviced as its own batch so the worker visits both in a single shift.
            foreach (var greenhouse in scopes.GreenhouseWorks)
                batches.Add(CreateSkeleton(greenhouse.LocationName, BatchKind.Greenhouse, greenhouseTasks, feedBuilding: false));
        }

        // Managed crops (U-MC-07): run the authored crop plan ahead of ordinary crop work in
        // the same live location. Non-farm managed locations precede greenhouse batches; farm
        // managed locations remain ahead of outdoor crop/clearing passes.
        EmitManagedCropBatches(
            batches,
            managedCropLocations.Where(location => string.Equals(location, "Farm", StringComparison.Ordinal)));

        var outdoorCropTasks = Order(enabledTasks.Where(TaskKindSets.IsOutdoorCropService));
        if (scopes.OutdoorWork is not null && outdoorCropTasks.Count > 0)
            batches.Add(CreateSkeleton("Farm", BatchKind.OutdoorCrops, outdoorCropTasks, feedBuilding: false));

        var outdoorClearingTasks = Order(enabledTasks.Where(TaskKindSets.IsOutdoorClearingService));
        if (scopes.OutdoorWork is not null && outdoorClearingTasks.Count > 0)
            batches.Add(CreateSkeleton("Farm", BatchKind.OutdoorClearing, outdoorClearingTasks, feedBuilding: false));

        return batches;
    }

    private static IReadOnlyList<TaskKind> Order(IEnumerable<TaskKind> tasks) =>
        new TaskPriorityOrderer().Order(tasks);

    private static IReadOnlyList<string> ManagedCropLocations(ManagedCropWorkScope? managedCrops)
    {
        if (managedCrops is not { IsEnabled: true })
            return Array.Empty<string>();

        return managedCrops.Assignments
            .Select(assignment => assignment.Zone.LocationName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();
    }

    private static void EmitManagedCropBatches(
        List<WorkBatch> batches,
        IEnumerable<string> locations)
    {
        foreach (var location in locations)
            batches.Add(CreateSkeleton(location, BatchKind.ManagedCrops, Array.Empty<TaskKind>(), feedBuilding: false));
    }

    private static WorkBatch CreateSkeleton(
        string locationName,
        BatchKind kind,
        IReadOnlyList<TaskKind> tasks,
        bool feedBuilding) =>
        new(
            locationName,
            kind,
            tasks,
            Array.Empty<WorkItem>(),
            Array.Empty<AnimalWorkItem>(),
            feedBuilding);
}
