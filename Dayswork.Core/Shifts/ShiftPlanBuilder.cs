using Dayswork.Core.Crops;
using Dayswork.Core.Domain;
using Dayswork.Core.Machines;

namespace Dayswork.Core.Shifts;

public sealed class ShiftPlanBuilder
{
    public IReadOnlyList<WorkBatch> BuildBatchPlan(
        WorkScopeSet scopes,
        IReadOnlySet<TaskKind> enabledTasks,
        IReadOnlyList<TaskCategory> categoryPriority)
    {
        if (scopes is null) throw new ArgumentNullException(nameof(scopes));
        if (enabledTasks is null) throw new ArgumentNullException(nameof(enabledTasks));
        if (categoryPriority is null) throw new ArgumentNullException(nameof(categoryPriority));

        var animalBatches  = BuildAnimalCareBatches(scopes, enabledTasks);
        var cropBatches    = BuildCropsBatches(scopes, enabledTasks);
        var fieldworkBatches = BuildFieldworkBatches(scopes, enabledTasks);
        var machineBatches = BuildMachineBatches(scopes);

        var result = new List<WorkBatch>(
            animalBatches.Count + cropBatches.Count + fieldworkBatches.Count + machineBatches.Count);
        foreach (var category in categoryPriority.Distinct())
        {
            result.AddRange(category switch
            {
                TaskCategory.AnimalCare => animalBatches,
                TaskCategory.Crops      => cropBatches,
                TaskCategory.Fieldwork  => fieldworkBatches,
                TaskCategory.Machines   => machineBatches,
                _                       => (IReadOnlyList<WorkBatch>)Array.Empty<WorkBatch>(),
            });
        }
        return result;
    }

    private static List<WorkBatch> BuildMachineBatches(WorkScopeSet scopes)
    {
        var batches = new List<WorkBatch>();
        if (scopes.Machines is not { IsEnabled: true } machines)
            return batches;

        // Flatten all groups' machines and bucket by location: one Machines batch per location with
        // ≥1 selected machine. Each machine's owning group/config is re-looked-up at runtime.
        var locations = machines.AllMachines
            .Select(machine => machine.LocationName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal);

        foreach (var location in locations)
            batches.Add(CreateSkeleton(location, BatchKind.Machines, Array.Empty<TaskKind>(), feedBuilding: false));

        return batches;
    }

    private static List<WorkBatch> BuildAnimalCareBatches(WorkScopeSet scopes, IReadOnlySet<TaskKind> enabledTasks)
    {
        var batches = new List<WorkBatch>();
        var animalTasks = Order(enabledTasks.Where(TaskKindSets.IsAnimalService));
        var outdoorAnimalTasks = animalTasks
            .Where(task => task != TaskKind.FeedAnimals)
            .ToList();

        if (animalTasks.Count == 0)
            return batches;

        // Per-building grouping: emit each building's interior batch immediately
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

        return batches;
    }

    private static List<WorkBatch> BuildCropsBatches(WorkScopeSet scopes, IReadOnlySet<TaskKind> enabledTasks)
    {
        var batches = new List<WorkBatch>();
        var managedCropLocations = ManagedCropLocations(scopes.ManagedCrops);

        EmitManagedCropBatches(
            batches,
            managedCropLocations.Where(location => !string.Equals(location, "Farm", StringComparison.Ordinal)));

        var greenhouseTasks = Order(enabledTasks.Where(TaskKindSets.IsGreenhouseService));
        if (greenhouseTasks.Count > 0)
        {
            // One greenhouse batch per selected greenhouse: a farm may expose the vanilla
            // greenhouse and an expansion greenhouse (e.g. SVE's Grandpa's Shed) at once; each is
            // serviced as its own batch so the worker visits both in a single shift.
            foreach (var greenhouse in scopes.GreenhouseWorks)
                batches.Add(CreateSkeleton(greenhouse.LocationName, BatchKind.Greenhouse, greenhouseTasks, feedBuilding: false));
        }

        // Managed crops: run the authored crop plan ahead of ordinary crop work in
        // the same live location. Non-farm managed locations precede greenhouse batches; farm
        // managed locations remain ahead of outdoor crop passes.
        EmitManagedCropBatches(
            batches,
            managedCropLocations.Where(location => string.Equals(location, "Farm", StringComparison.Ordinal)));

        var outdoorCropTasks = Order(enabledTasks.Where(TaskKindSets.IsOutdoorCropService));
        if (scopes.OutdoorWork is not null && outdoorCropTasks.Count > 0)
            batches.Add(CreateSkeleton("Farm", BatchKind.OutdoorCrops, outdoorCropTasks, feedBuilding: false));

        return batches;
    }

    private static List<WorkBatch> BuildFieldworkBatches(WorkScopeSet scopes, IReadOnlySet<TaskKind> enabledTasks)
    {
        var batches = new List<WorkBatch>();
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
