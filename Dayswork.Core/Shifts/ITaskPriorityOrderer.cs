using Dayswork.Core.Domain;

namespace Dayswork.Core.Shifts;

/// <summary>
/// Orders a set of enabled tasks by the contract's player-defined category priority.
/// </summary>
public interface ITaskPriorityOrderer
{
    /// <summary>
    /// Returns only the provided tasks, in category-priority order (ties broken by enum order).
    /// Tasks absent from the input are absent from the output.
    /// Empty input returns an empty list.
    /// </summary>
    IReadOnlyList<TaskKind> Order(IEnumerable<TaskKind> enabledTasks);

    /// <summary>
    /// Returns the priority rank for a task (its category's index in the contract's order).
    /// Lower numbers run first; same-category tasks share a rank, so route cost breaks the tie.
    /// </summary>
    int Rank(TaskKind task);
}
