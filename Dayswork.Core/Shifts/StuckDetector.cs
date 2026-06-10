namespace Dayswork.Core.Shifts;

/// <summary>
/// Accumulates no-progress in-game minutes and fires when the threshold is reached.
/// Pure Core — zero Stardew/SMAPI references.
///
/// Invariants (property-tested):
///   - Any tick with madeProgress=true resets the accumulator → ShouldFireStuck() false.
///   - With only no-progress ticks, ShouldFireStuck() is false while sum &lt; threshold
///     and true once sum &gt;= threshold.
///   - Reset() always returns the detector to the not-stuck state.
/// </summary>
public sealed class StuckDetector
{
    private readonly int _threshold;
    private int _noProgressMinutes;

    /// <param name="thresholdMinutes">
    /// In-game minutes of continuous no-progress before <see cref="ShouldFireStuck"/> returns true.
    /// Pass <see cref="Core.Config.ConfigSnapshot.StuckInitialWaitMinutes"/> initially;
    /// create a new instance with <see cref="Core.Config.ConfigSnapshot.StuckPostTeleportWaitMinutes"/>
    /// after the first teleport recovery.
    /// </param>
    public StuckDetector(int thresholdMinutes)
    {
        if (thresholdMinutes <= 0)
            throw new ArgumentOutOfRangeException(nameof(thresholdMinutes),
                "Stuck threshold must be positive.");
        _threshold = thresholdMinutes;
    }

    /// <inheritdoc/>
    public void RecordTick(bool madeProgress, int elapsedMinutes)
    {
        if (madeProgress)
        {
            _noProgressMinutes = 0;
        }
        else
        {
            _noProgressMinutes += elapsedMinutes;
        }
    }

    /// <inheritdoc/>
    public bool ShouldFireStuck() => _noProgressMinutes >= _threshold;

    /// <inheritdoc/>
    public void Reset() => _noProgressMinutes = 0;
}
