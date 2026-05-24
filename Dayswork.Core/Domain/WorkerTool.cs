namespace Dayswork.Core.Domain;

public enum WorkerTool
{
    None,
    WateringCan,
    Scythe,
    Pickaxe,
    Axe,
    MilkPail,
    Shears,
}

public static class WorkerToolExtensions
{
    public static WorkerTool ForTask(TaskKind task) =>
        task switch
        {
            TaskKind.WaterCrops => WorkerTool.WateringCan,
            TaskKind.ClearWeeds or TaskKind.ClearGrass => WorkerTool.Scythe,
            TaskKind.ClearRocks => WorkerTool.Pickaxe,
            TaskKind.CutTrees => WorkerTool.Axe,
            TaskKind.HarvestCrops
                or TaskKind.CollectFruit
                or TaskKind.FeedAnimals
                or TaskKind.PetAnimals
                or TaskKind.CollectAnimalProducts => WorkerTool.None,
            _ => WorkerTool.None,
        };
}
