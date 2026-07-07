namespace Dayswork.Core.Shifts;

public enum ShiftStopReason
{
    None,
    Completed,
    Exhausted,
    HardCap,
    Sleep,
    StuckAbort,
    Cancelled,

    /// <summary>
    /// The worker wrapped up early because the next unit of work couldn't start, be serviced, and be
    /// walked home from before the 8pm hard cap (see <see cref="ShiftClockEstimator"/>). Distinct from
    /// <see cref="HardCap"/> (which is the 8pm backstop firing mid-trip) so the HUD/log can explain the
    /// worker went home to avoid a wasted round trip.
    /// </summary>
    DayEndingSoon,
}
