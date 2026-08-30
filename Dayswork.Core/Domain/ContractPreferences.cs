namespace Dayswork.Core.Domain;

public sealed record ContractPreferences(
    bool AvoidBlueGrass = true,
    IdleTaskKind IdleTask = IdleTaskKind.ManageMachines,
    string WorkerName = "")
{
    public static readonly ContractPreferences Default = new();

    // Contracts from before preferences were introduced: preserve the original behavior
    // (clear all grass, including blue grass). Idle task is pinned to None — old contracts go home
    // when their work is done, exactly as they did before the idle loop existed, regardless of what
    // the default for NEW contracts is.
    public static readonly ContractPreferences Legacy =
        new(AvoidBlueGrass: false, IdleTask: IdleTaskKind.None);
}
