namespace Dayswork.Core.Domain;

/// <summary>
/// What the farmhand does once all first-round contract work is finished, before going home.
/// <see cref="None"/> keeps the original behavior (return to the office and leave). Other values
/// name a repeatable task the worker keeps performing until energy exhaustion or the day's hard
/// time cap. v1 ships only <see cref="ManageMachines"/>; the enum is open for more later.
/// </summary>
public enum IdleTaskKind
{
    None = 0,
    ManageMachines = 1,
}
