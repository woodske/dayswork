namespace Dayswork.Core.Shifts;

/// <summary>
/// Estimates how much in-game time a walk consumes, so the shift can decide whether a late-day trip
/// can start, be serviced, and get home before the 8pm hard cap — instead of walking to a distant
/// shed at 7:55pm only to be yanked home by the cap. Deliberately coarse; rounds pessimistically.
///
/// <para>Conversion constants verified against <c>Stardew Valley.dll</c> (2026-07-07): the clock
/// advances 10 in-game minutes every 7000 real ms (<c>realMilliSecondsPerGameMinute = 700</c>), and
/// <c>gameTimeInterval</c> accrues <c>ElapsedGameTime.Milliseconds</c> each tick at MonoGame's fixed
/// 60 UPS (~16.667 ms/tick) → ~42 ticks per in-game minute. A location can only *slow* time (via
/// <c>ExtraMillisecondsPerInGameMinute</c>), never speed it up, so ignoring it makes the estimate
/// more pessimistic — which is the safe direction for a "will it fit" gate. See
/// <c>docs/time-and-pacing.md</c>.</para>
/// </summary>
public static class ShiftClockEstimator
{
    /// <summary>Real milliseconds per in-game minute (Stardew's <c>realMilliSecondsPerGameMinute</c>).</summary>
    public const double RealMsPerInGameMinute = 700.0;

    /// <summary>Real milliseconds per update tick at MonoGame's fixed 60 updates/second.</summary>
    public const double RealMsPerTick = 1000.0 / 60.0;

    /// <summary>Pixels per tile.</summary>
    public const double TilePixels = 64.0;

    /// <summary>
    /// Estimated in-game minutes to walk <paramref name="tiles"/> tiles at
    /// <paramref name="walkPixelsPerTick"/> pixels/tick. Pessimistic (ceils). Zero-length ⇒ 0; a
    /// non-positive speed is degenerate and returns <see cref="int.MaxValue"/> (never fits).
    /// </summary>
    public static int EstimateWalkMinutes(int tiles, float walkPixelsPerTick)
    {
        if (tiles <= 0) return 0;
        if (walkPixelsPerTick <= 0f) return int.MaxValue;

        var ticks = tiles * TilePixels / walkPixelsPerTick;
        var minutes = ticks * RealMsPerTick / RealMsPerInGameMinute;
        return (int)Math.Ceiling(minutes);
    }

    /// <summary>
    /// Add <paramref name="minutes"/> in-game minutes to a Stardew <paramref name="timeOfDay"/>
    /// (encoded HHMM, minutes 0–59 rolling into the next hour). Negative minutes are clamped to 0.
    /// </summary>
    public static int AddInGameMinutes(int timeOfDay, int minutes)
    {
        if (minutes <= 0) return timeOfDay;

        var total = timeOfDay / 100 * 60 + timeOfDay % 100 + minutes;
        return total / 60 * 100 + total % 60;
    }

    /// <summary>
    /// Whether the worker should start a trip now: current <paramref name="timeOfDay"/> plus the
    /// estimated walk out + a headroom reserve (arrive, do at least one work beat, walk home) must
    /// land at or before <paramref name="hardCapTimeOfDay"/> (8pm = 2000). <paramref name="homeboundTiles"/>
    /// is the walk back to the farm exit from the destination.
    /// </summary>
    public static bool FitsBeforeCap(
        int timeOfDay,
        int hardCapTimeOfDay,
        int outboundTiles,
        int homeboundTiles,
        float walkPixelsPerTick,
        int workHeadroomMinutes)
    {
        var outbound = EstimateWalkMinutes(outboundTiles, walkPixelsPerTick);
        var homebound = EstimateWalkMinutes(homeboundTiles, walkPixelsPerTick);
        if (outbound == int.MaxValue || homebound == int.MaxValue)
            return false;

        var needed = outbound + homebound + Math.Max(0, workHeadroomMinutes);
        return AddInGameMinutes(timeOfDay, needed) <= hardCapTimeOfDay;
    }
}
