namespace Dayswork.Core.Machines;

/// <summary>
/// The planner's output for one reload pass: which machines get loaded with which recipe, the exact
/// per-id withdrawal totals to pull from the input chest, and the machines left unsatisfied because
/// supply ran dry (skipped with a HUD note by the executor).
/// </summary>
public sealed record MachineLoadPlan
{
    public static MachineLoadPlan Empty { get; } = new(
        Array.Empty<MachineLoadAssignment>(),
        new Dictionary<string, int>(StringComparer.Ordinal),
        Array.Empty<MachineRef>());

    public IReadOnlyList<MachineLoadAssignment> Assignments { get; }
    public IReadOnlyDictionary<string, int> Withdrawals { get; }
    public IReadOnlyList<MachineRef> Unsatisfied { get; }

    public MachineLoadPlan(
        IReadOnlyList<MachineLoadAssignment> assignments,
        IReadOnlyDictionary<string, int> withdrawals,
        IReadOnlyList<MachineRef> unsatisfied)
    {
        Assignments = assignments;
        Withdrawals = withdrawals;
        Unsatisfied = unsatisfied;
    }

    public bool HasWork => Assignments.Count > 0;
}
