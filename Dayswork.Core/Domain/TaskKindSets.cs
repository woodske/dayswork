namespace Dayswork.Core.Domain;

public static class TaskKindSets
{
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
}
