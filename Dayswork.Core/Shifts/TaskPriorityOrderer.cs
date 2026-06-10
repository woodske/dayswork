using Dayswork.Core.Domain;

namespace Dayswork.Core.Shifts;

/// <summary>
/// Orders enabled tasks by the contract's player-chosen category priority.
/// </summary>
public sealed class TaskPriorityOrderer
{
    // Lower rank = higher priority. Rank is the index of a task's category in the contract's
    // player-ordered category list; tasks sharing a category share a rank, so route cost
    // (distance) breaks ties — i.e. strict ordering between categories, nearest-first within one.
    private readonly IReadOnlyDictionary<TaskCategory, int> _categoryRank;

    public TaskPriorityOrderer(IReadOnlyList<TaskCategory> categoryPriority)
    {
        var rank = new Dictionary<TaskCategory, int>();
        for (var i = 0; i < categoryPriority.Count; i++)
            rank.TryAdd(categoryPriority[i], i);

        // Any category the player order omits sorts last, deterministically.
        foreach (var category in Enum.GetValues<TaskCategory>())
            rank.TryAdd(category, categoryPriority.Count);

        _categoryRank = rank;
    }

    /// <summary>Uses the default category priority (<see cref="TaskKindSets.DefaultCategoryPriority"/>).</summary>
    public TaskPriorityOrderer() : this(TaskKindSets.DefaultCategoryPriority) { }

    public IReadOnlyList<TaskKind> Order(IEnumerable<TaskKind> enabledTasks) =>
        enabledTasks
            .OrderBy(task => _categoryRank[TaskKindSets.CategoryOf(task)])
            .ThenBy(task => task)
            .ToList();

    public int Rank(TaskKind task) => _categoryRank[TaskKindSets.CategoryOf(task)];
}
