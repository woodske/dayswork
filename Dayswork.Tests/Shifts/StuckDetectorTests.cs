using Dayswork.Core.Shifts;
using FsCheck;
using FsCheck.Xunit;
using Xunit;

namespace Dayswork.Tests.Shifts;

/// <summary>
/// PBT-U13-04/05/06  —  StuckDetector pure-Core properties.
/// Seed + shrunk-input logging is provided by FsCheck.Xunit by default (PBT-08 / PBT-U13-07).
/// To replay a known failure: [Property(Replay = "(seed1, seed2)")].
/// </summary>
public sealed class StuckDetectorTests
{
    private const int DefaultThreshold = 10;

    // ── PBT-U13-04: any progress tick resets → not stuck immediately after. ──
    [Property(MaxTest = 1000, Replay = "")]
    public Property Progress_Tick_Resets_Accumulator()
    {
        // Generate: list of no-progress minutes that may push accumulator toward threshold,
        // then a final progress tick.
        var noProgressGen =
            Gen.ListOf(Gen.Choose(1, 20))
               .Select(list => list.Take(10).ToList()); // cap length for speed

        return Prop.ForAll(noProgressGen.ToArbitrary(), noProgressList =>
        {
            var det = new StuckDetector(DefaultThreshold);

            // Accumulate some no-progress time.
            foreach (var mins in noProgressList)
                det.RecordTick(madeProgress: false, elapsedMinutes: mins);

            // A single progress tick must reset → not stuck.
            det.RecordTick(madeProgress: true, elapsedMinutes: 1);

            return (!det.ShouldFireStuck())
                .Label("ShouldFireStuck must be false immediately after any progress tick");
        });
    }

    // ── PBT-U13-05: no-progress accumulation is monotone w.r.t. threshold. ──
    [Property(MaxTest = 1000, Replay = "")]
    public Property NoProgress_Threshold_Monotonicity()
    {
        // Generate a positive threshold and a list of positive elapsed-minute values.
        var thresholdGen = Gen.Choose(1, 60);
        var tickListGen  = Gen.NonEmptyListOf(Gen.Choose(1, 10));

        return Prop.ForAll(thresholdGen.ToArbitrary(), tickListGen.ToArbitrary(),
            (threshold, ticks) =>
            {
                var det = new StuckDetector(threshold);
                int accumulated = 0;

                foreach (var mins in ticks)
                {
                    bool wasStuckBefore = accumulated >= threshold;
                    det.RecordTick(madeProgress: false, elapsedMinutes: mins);
                    accumulated += mins;
                    bool isStuckNow = det.ShouldFireStuck();

                    if (accumulated < threshold && isStuckNow)
                        return false.Label(
                            $"ShouldFireStuck true too early: {accumulated} < threshold {threshold}");

                    if (accumulated >= threshold && !isStuckNow)
                        return false.Label(
                            $"ShouldFireStuck false too late: {accumulated} >= threshold {threshold}");
                }
                return true.ToProperty();
            });
    }

    // ── PBT-U13-06: Reset() always returns detector to not-stuck. ────────────
    [Property(MaxTest = 1000, Replay = "")]
    public Property Reset_Always_Clears()
    {
        var noProgressGen =
            Gen.ListOf(Gen.Choose(1, 30))
               .Select(list => list.Take(20).ToList());

        return Prop.ForAll(noProgressGen.ToArbitrary(), noProgressList =>
        {
            var det = new StuckDetector(DefaultThreshold);

            // Drive the accumulator as high as the generated list allows.
            foreach (var mins in noProgressList)
                det.RecordTick(madeProgress: false, elapsedMinutes: mins);

            det.Reset();

            return (!det.ShouldFireStuck())
                .Label("ShouldFireStuck must be false after Reset() regardless of prior accumulation");
        });
    }

    // ── Sanity facts ─────────────────────────────────────────────────────────

    [Fact]
    public void Not_Stuck_Initially()
    {
        var det = new StuckDetector(DefaultThreshold);
        Assert.False(det.ShouldFireStuck());
    }

    [Fact]
    public void Fires_Exactly_At_Threshold()
    {
        var det = new StuckDetector(10);
        det.RecordTick(false, 9);
        Assert.False(det.ShouldFireStuck());
        det.RecordTick(false, 1);
        Assert.True(det.ShouldFireStuck());
    }

    [Fact]
    public void Progress_After_Becoming_Stuck_Clears_The_Stuck_State_Immediately()
    {
        var det = new StuckDetector(10);
        det.RecordTick(false, 10);
        Assert.True(det.ShouldFireStuck());

        det.RecordTick(true, 1);

        Assert.False(det.ShouldFireStuck());
    }

    [Fact]
    public void Constructor_Rejects_Non_Positive_Threshold()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new StuckDetector(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new StuckDetector(-5));
    }
}
