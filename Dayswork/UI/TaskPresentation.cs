using Dayswork.Core.Domain;

namespace Dayswork.UI;

internal static class TaskPresentation
{
    public static readonly TaskKind[] TaskOrder =
    {
        TaskKind.WaterCrops,
        TaskKind.HarvestCrops,
        TaskKind.CollectFruit,
        TaskKind.FeedAnimals,
        TaskKind.PetAnimals,
        TaskKind.CollectAnimalProducts,
        TaskKind.CutTrees,
        TaskKind.ClearRocks,
        TaskKind.ClearWeeds,
        TaskKind.ClearGrass,
    };

    public static string GetI18nKey(TaskKind task) => task switch
    {
        TaskKind.WaterCrops => "ui.task_selection.water_crops",
        TaskKind.HarvestCrops => "ui.task_selection.harvest_crops",
        TaskKind.CollectFruit => "ui.task_selection.collect_fruit",
        TaskKind.FeedAnimals => "ui.task_selection.feed_animals",
        TaskKind.PetAnimals => "ui.task_selection.pet_animals",
        TaskKind.CollectAnimalProducts => "ui.task_selection.collect_animal_products",
        TaskKind.CutTrees => "ui.task_selection.cut_trees",
        TaskKind.ClearRocks => "ui.task_selection.clear_rocks",
        TaskKind.ClearWeeds => "ui.task_selection.clear_weeds",
        TaskKind.ClearGrass => "ui.task_selection.clear_grass",
        _ => task.ToString(),
    };
}
