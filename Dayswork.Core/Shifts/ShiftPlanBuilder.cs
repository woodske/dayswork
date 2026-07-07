using Dayswork.Core.Crops;
using Dayswork.Core.Domain;
using Dayswork.Core.FishPonds;
using Dayswork.Core.Machines;

namespace Dayswork.Core.Shifts;

public sealed class ShiftPlanBuilder
{
    public IReadOnlyList<WorkBatch> BuildBatchPlan(
        WorkScopeSet scopes,
        IReadOnlySet<TaskKind> enabledTasks,
        IReadOnlyList<TaskCategory> categoryPriority,
        BatchOrderingContext? ordering = null)
    {
        if (scopes is null) throw new ArgumentNullException(nameof(scopes));
        if (enabledTasks is null) throw new ArgumentNullException(nameof(enabledTasks));
        if (categoryPriority is null) throw new ArgumentNullException(nameof(categoryPriority));

        var animalBatches  = BuildAnimalCareBatches(scopes, enabledTasks, ordering);
        var cropBatches    = BuildCropsBatches(scopes, enabledTasks, ordering);
        var fieldworkBatches = BuildFieldworkBatches(scopes, enabledTasks);
        var machineBatches = BuildMachineBatches(scopes, ordering);
        var fishPondBatches = BuildFishPondBatches(scopes, ordering);

        var result = new List<WorkBatch>(
            animalBatches.Count + cropBatches.Count + fieldworkBatches.Count + machineBatches.Count + fishPondBatches.Count);
        foreach (var category in categoryPriority.Distinct())
        {
            result.AddRange(category switch
            {
                TaskCategory.AnimalCare => animalBatches,
                TaskCategory.Crops      => cropBatches,
                TaskCategory.Fieldwork  => fieldworkBatches,
                TaskCategory.Machines   => machineBatches,
                TaskCategory.FishPonds  => fishPondBatches,
                _                       => (IReadOnlyList<WorkBatch>)Array.Empty<WorkBatch>(),
            });
        }
        return result;
    }

    /// <summary>
    /// Just the machine batches (one per location with selected machines), in the same order the
    /// full plan would place them. Used by the idle loop to re-run machine work after first-round
    /// work is finished.
    /// </summary>
    public IReadOnlyList<WorkBatch> BuildMachineBatchPlan(WorkScopeSet scopes, BatchOrderingContext? ordering = null)
    {
        if (scopes is null) throw new ArgumentNullException(nameof(scopes));
        return BuildMachineBatches(scopes, ordering);
    }

    private static List<WorkBatch> BuildMachineBatches(WorkScopeSet scopes, BatchOrderingContext? ordering)
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
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        // Nearest-neighbor over per-location batches (was pure name order — the easiest travel win).
        foreach (var location in OrderLocationsByTravel(locations, ordering))
            batches.Add(CreateSkeleton(location, BatchKind.Machines, Array.Empty<TaskKind>(), feedBuilding: false));

        return batches;
    }

    private static List<WorkBatch> BuildFishPondBatches(WorkScopeSet scopes, BatchOrderingContext? ordering)
    {
        var batches = new List<WorkBatch>();
        if (scopes.FishPonds is not { IsEnabled: true } fishPonds)
            return batches;

        // Bucket selected ponds by location: one FishPonds batch per location with ≥1 pond. Each
        // pond is re-resolved at runtime; the scope's single output destination applies to all.
        var locations = fishPonds.Ponds
            .Select(pond => pond.LocationName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        foreach (var location in OrderLocationsByTravel(locations, ordering))
            batches.Add(CreateSkeleton(location, BatchKind.FishPonds, Array.Empty<TaskKind>(), feedBuilding: false));

        return batches;
    }

    private static List<WorkBatch> BuildAnimalCareBatches(WorkScopeSet scopes, IReadOnlySet<TaskKind> enabledTasks, BatchOrderingContext? ordering)
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
        //
        // Buildings are visited nearest-neighbor by their outdoor door tile; each building's
        // interior+grazing pair moves together as one unit (so "fully service one building before
        // the next" survives the reorder). No ordering context ⇒ today's name+tier order.
        var buildings = scopes.AnimalBuildings
            .OrderBy(scope => scope.LocationName, StringComparer.Ordinal)
            .ThenBy(scope => scope.Tier)
            .ToList();

        foreach (var building in OrderByTravel(buildings, scope => scope.LocationName, ordering))
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

    private static List<WorkBatch> BuildCropsBatches(WorkScopeSet scopes, IReadOnlySet<TaskKind> enabledTasks, BatchOrderingContext? ordering)
    {
        var batches = new List<WorkBatch>();
        var managedCropLocations = ManagedCropLocations(scopes.ManagedCrops);

        // Crops-category internal *structure* is fixed: managed non-farm → greenhouses → managed
        // farm → outdoor crops → FarmCave. Within the multi-location slots (managed non-farm,
        // greenhouses) batches are nearest-neighbor ordered when a context is supplied.
        EmitManagedCropBatches(
            batches,
            OrderLocationsByTravel(
                managedCropLocations.Where(location => !string.Equals(location, "Farm", StringComparison.Ordinal)).ToList(),
                ordering));

        var greenhouseTasks = Order(enabledTasks.Where(TaskKindSets.IsGreenhouseService));
        if (greenhouseTasks.Count > 0)
        {
            // One greenhouse batch per selected greenhouse: a farm may expose the vanilla
            // greenhouse and an expansion greenhouse (e.g. SVE's Grandpa's Shed) at once; each is
            // serviced as its own batch so the worker visits both in a single shift.
            foreach (var greenhouse in OrderByTravel(scopes.GreenhouseWorks.ToList(), g => g.LocationName, ordering))
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

        if (enabledTasks.Contains(TaskKind.HarvestCave))
            batches.Add(CreateSkeleton("FarmCave", BatchKind.FarmCave,
                new[] { TaskKind.HarvestCave }, feedBuilding: false));

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

    private static IReadOnlyList<string> OrderLocationsByTravel(IReadOnlyList<string> locations, BatchOrderingContext? ordering) =>
        OrderByTravel(locations, name => name, ordering);

    /// <summary>
    /// Nearest-neighbor chain <paramref name="items"/> by their location's anchor tile, starting from
    /// the worker's spawn anchor and hopping to the nearest remaining anchor each step (Manhattan
    /// distance; ties break by location name ordinal, so output is deterministic). Items whose
    /// location has no anchor sort last in their incoming (name) order. With no ordering context the
    /// input order is returned unchanged — the pre-cache alphabetical behaviour.
    /// </summary>
    private static IReadOnlyList<T> OrderByTravel<T>(
        IReadOnlyList<T> items,
        Func<T, string> locationOf,
        BatchOrderingContext? ordering)
    {
        if (ordering is null || items.Count <= 1)
            return items;

        var anchored = new List<T>();
        var unanchored = new List<T>();
        foreach (var item in items)
        {
            if (ordering.Anchors.ContainsKey(locationOf(item)))
                anchored.Add(item);
            else
                unanchored.Add(item);
        }

        var result = new List<T>(items.Count);
        var current = ordering.StartAnchor;
        while (anchored.Count > 0)
        {
            var bestIdx = 0;
            var bestAnchor = ordering.Anchors[locationOf(anchored[0])];
            var bestDist = Manhattan(current, bestAnchor);
            for (var i = 1; i < anchored.Count; i++)
            {
                var anchor = ordering.Anchors[locationOf(anchored[i])];
                var dist = Manhattan(current, anchor);
                if (dist < bestDist ||
                    (dist == bestDist &&
                     string.CompareOrdinal(locationOf(anchored[i]), locationOf(anchored[bestIdx])) < 0))
                {
                    bestIdx = i;
                    bestDist = dist;
                    bestAnchor = anchor;
                }
            }

            result.Add(anchored[bestIdx]);
            current = bestAnchor;
            anchored.RemoveAt(bestIdx);
        }

        result.AddRange(unanchored); // deterministic degradation: name order, after the anchored ones
        return result;
    }

    private static int Manhattan(TileCoord a, TileCoord b) =>
        Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);

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
