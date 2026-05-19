using Dayswork.Core.Domain;

namespace Dayswork.Core.Shifts;

/// <summary>
/// Orders a set of enabled tasks according to the fixed FR-WORK-03 priority sequence.
/// </summary>
public interface ITaskPriorityOrderer
{
    /// <summary>
    /// Returns only the provided tasks, in FR-WORK-03 priority order.
    /// Tasks absent from the input are absent from the output.
    /// Empty input returns an empty list.
    /// </summary>
    IReadOnlyList<TaskKind> Order(IEnumerable<TaskKind> enabledTasks);
}
