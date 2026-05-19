using Dayswork.Core.Domain;

namespace Dayswork.Core.Shifts;

/// <inheritdoc/>
public sealed class TaskPriorityOrderer : ITaskPriorityOrderer
{
    // FR-WORK-03 priority table — lower number = higher priority
    private static readonly Dictionary<TaskKind, int> s_rank = new Dictionary<TaskKind, int>
    {
        { TaskKind.FeedAnimals,            0 },
        { TaskKind.PetAnimals,             1 },
        { TaskKind.CollectAnimalProducts,  2 },
        { TaskKind.WaterCrops,             3 },
        { TaskKind.HarvestCrops,           4 },
        { TaskKind.CollectFruit,           5 },
        { TaskKind.ClearWeeds,             6 },
        { TaskKind.ClearGrass,             7 },
        { TaskKind.ClearRocks,             8 },
        { TaskKind.CutTrees,               9 },
    };

    /// <inheritdoc/>
    public IReadOnlyList<TaskKind> Order(IEnumerable<TaskKind> enabledTasks) =>
        enabledTasks.OrderBy(t => s_rank[t]).ToList();
}
