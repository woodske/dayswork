namespace Dayswork.Core.Shifts;

/// <summary>
/// Accumulates no-progress time and signals when the worker should be considered stuck.
/// Pure Core — no Stardew/SMAPI references (MAINT-U13-01).
/// </summary>
public interface IStuckDetector
{
    /// <summary>
    /// Record one sampled tick. If <paramref name="madeProgress"/> is true the accumulator
    /// resets; otherwise <paramref name="elapsedMinutes"/> is added to it.
    /// </summary>
    void RecordTick(bool madeProgress, int elapsedMinutes);

    /// <summary>Returns true when the no-progress accumulator has reached the threshold.</summary>
    bool ShouldFireStuck();

    /// <summary>Resets the accumulator to zero without changing the threshold.</summary>
    void Reset();
}
