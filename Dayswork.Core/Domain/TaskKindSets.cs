namespace Dayswork.Core.Domain;

public static class TaskKindSets
{
    public static IReadOnlyList<TaskKind> OutdoorCropServices { get; } = new[]
    {
        TaskKind.WaterCrops,
        TaskKind.HarvestCrops,
        TaskKind.CollectFruit,
    };

    public static IReadOnlyList<TaskKind> OutdoorClearingServices { get; } = new[]
    {
        TaskKind.CutTrees,
        TaskKind.ClearRocks,
        TaskKind.ClearWeeds,
        TaskKind.ClearGrass,
    };

    public static IReadOnlyList<TaskKind> OutdoorServices { get; } = new[]
    {
        TaskKind.WaterCrops,
        TaskKind.HarvestCrops,
        TaskKind.CollectFruit,
        TaskKind.CutTrees,
        TaskKind.ClearRocks,
        TaskKind.ClearWeeds,
        TaskKind.ClearGrass,
    };

    public static IReadOnlyList<TaskKind> AnimalServices { get; } = new[]
    {
        TaskKind.FeedAnimals,
        TaskKind.PetAnimals,
        TaskKind.CollectAnimalProducts,
    };

    public static IReadOnlyList<TaskKind> GreenhouseServices { get; } = new[]
    {
        TaskKind.WaterCrops,
        TaskKind.HarvestCrops,
        TaskKind.CollectFruit,
    };

    public static bool IsCaveService(TaskKind kind) => kind == TaskKind.HarvestCave;

    public static bool IsOutdoorService(TaskKind kind) => kind switch
    {
        TaskKind.WaterCrops => true,
        TaskKind.HarvestCrops => true,
        TaskKind.CollectFruit => true,
        TaskKind.CutTrees => true,
        TaskKind.ClearRocks => true,
        TaskKind.ClearWeeds => true,
        TaskKind.ClearGrass => true,
        _ => false,
    };

    public static bool IsOutdoorCropService(TaskKind kind) => kind switch
    {
        TaskKind.WaterCrops => true,
        TaskKind.HarvestCrops => true,
        TaskKind.CollectFruit => true,
        _ => false,
    };

    public static bool IsOutdoorClearingService(TaskKind kind) => kind switch
    {
        TaskKind.CutTrees => true,
        TaskKind.ClearRocks => true,
        TaskKind.ClearWeeds => true,
        TaskKind.ClearGrass => true,
        _ => false,
    };

    public static bool IsAnimalService(TaskKind kind) => kind switch
    {
        TaskKind.FeedAnimals => true,
        TaskKind.PetAnimals => true,
        TaskKind.CollectAnimalProducts => true,
        _ => false,
    };

    public static bool IsGreenhouseService(TaskKind kind) => kind switch
    {
        TaskKind.WaterCrops => true,
        TaskKind.HarvestCrops => true,
        TaskKind.CollectFruit => true,
        _ => false,
    };

    /// <summary>The default category priority order for a new contract (highest priority first).</summary>
    public static IReadOnlyList<TaskCategory> DefaultCategoryPriority { get; } = new[]
    {
        TaskCategory.AnimalCare,
        TaskCategory.Crops,
        TaskCategory.Fieldwork,
        TaskCategory.Machines,
        TaskCategory.FishPonds,
    };

    /// <summary>
    /// Maps a task to its coarse priority category. Watering is folded into <see cref="TaskCategory.Crops"/>.
    /// Greenhouse crop services share the same crop <see cref="TaskKind"/> values and therefore the same category.
    /// </summary>
    public static TaskCategory CategoryOf(TaskKind kind) => kind switch
    {
        TaskKind.FeedAnimals => TaskCategory.AnimalCare,
        TaskKind.PetAnimals => TaskCategory.AnimalCare,
        TaskKind.CollectAnimalProducts => TaskCategory.AnimalCare,
        TaskKind.WaterCrops => TaskCategory.Crops,
        TaskKind.HarvestCrops => TaskCategory.Crops,
        TaskKind.CollectFruit => TaskCategory.Crops,
        TaskKind.HarvestCave => TaskCategory.Crops,
        TaskKind.CutTrees => TaskCategory.Fieldwork,
        TaskKind.ClearRocks => TaskCategory.Fieldwork,
        TaskKind.ClearWeeds => TaskCategory.Fieldwork,
        TaskKind.ClearGrass => TaskCategory.Fieldwork,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unmapped TaskKind has no priority category."),
    };
}
