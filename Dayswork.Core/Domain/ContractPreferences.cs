namespace Dayswork.Core.Domain;

public sealed record ContractPreferences(
    bool AvoidBlueGrass = true,
    IdleTaskKind IdleTask = IdleTaskKind.ManageMachines,
    string WorkerName = "",
    // 2.0 owner preferences. RunWhileOwnerOffline lets the worker keep working while its sponsor
    // is not connected; GrantExperience routes the XP the worker earns to the sponsor. Appearance
    // is the worker's colour variant ("" = the default sheet).
    bool RunWhileOwnerOffline = true,
    bool GrantExperience = true,
    string Appearance = "")
{
    public static readonly ContractPreferences Default = new();

    // Contracts from before preferences were introduced: preserve the original behavior
    // (clear all grass, including blue grass). Idle task is pinned to None — old contracts go home
    // when their work is done, exactly as they did before the idle loop existed, regardless of what
    // the default for NEW contracts is. The 2.0 preferences take their (new-behaviour) defaults:
    // an adopted 1.x contract is host-owned, so "run while offline" and "grant XP" are no-ops for it.
    public static readonly ContractPreferences Legacy =
        new(AvoidBlueGrass: false, IdleTask: IdleTaskKind.None);
}
