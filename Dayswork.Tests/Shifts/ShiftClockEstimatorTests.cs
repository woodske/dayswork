using Dayswork.Core.Shifts;
using Xunit;

namespace Dayswork.Tests.Shifts;

/// <summary>
/// ShiftClockEstimator conversion math + fit-before-cap boundaries (architecture review #5).
/// Constants verified against the decompile (700 ms/in-game-minute, 60 UPS ≈ 42 ticks/minute).
/// </summary>
public sealed class ShiftClockEstimatorTests
{
    [Fact]
    public void EstimateWalkMinutes_ZeroTiles_IsZero()
    {
        Assert.Equal(0, ShiftClockEstimator.EstimateWalkMinutes(0, 2f));
        Assert.Equal(0, ShiftClockEstimator.EstimateWalkMinutes(-5, 2f));
    }

    [Fact]
    public void EstimateWalkMinutes_NonPositiveSpeed_NeverFits()
    {
        Assert.Equal(int.MaxValue, ShiftClockEstimator.EstimateWalkMinutes(10, 0f));
        Assert.Equal(int.MaxValue, ShiftClockEstimator.EstimateWalkMinutes(10, -1f));
    }

    [Fact]
    public void EstimateWalkMinutes_UsesVerifiedConversion_AndCeils()
    {
        // 10 tiles @ 2 px/tick: ticks = 10*64/2 = 320; minutes = 320 * (1000/60) / 700 ≈ 7.62 → ceil 8.
        Assert.Equal(8, ShiftClockEstimator.EstimateWalkMinutes(10, 2f));

        // 1 tile @ 2 px/tick: ticks = 32; minutes = 32*16.667/700 ≈ 0.762 → ceil 1.
        Assert.Equal(1, ShiftClockEstimator.EstimateWalkMinutes(1, 2f));
    }

    [Fact]
    public void EstimateWalkMinutes_FasterSpeed_TakesLessTime()
    {
        var slow = ShiftClockEstimator.EstimateWalkMinutes(40, 2f);
        var fast = ShiftClockEstimator.EstimateWalkMinutes(40, 4f);
        Assert.True(fast < slow);
    }

    [Theory]
    [InlineData(1950, 10, 2000)] // 7:50pm + 10 → 8:00pm
    [InlineData(1955, 10, 2005)] // minutes roll within the hour
    [InlineData(1900, 60, 2000)] // a full hour
    [InlineData(1930, 0, 1930)]  // zero-minute add is identity
    [InlineData(1930, -5, 1930)] // negative clamps to identity
    public void AddInGameMinutes_RollsOverHoursCorrectly(int timeOfDay, int minutes, int expected)
    {
        Assert.Equal(expected, ShiftClockEstimator.AddInGameMinutes(timeOfDay, minutes));
    }

    [Fact]
    public void FitsBeforeCap_EarlyDay_Fits()
    {
        // 10am, short trip — plenty of headroom before 8pm.
        Assert.True(ShiftClockEstimator.FitsBeforeCap(
            timeOfDay: 1000, hardCapTimeOfDay: 2000,
            outboundTiles: 20, homeboundTiles: 20, walkPixelsPerTick: 2f, workHeadroomMinutes: 10));
    }

    [Fact]
    public void FitsBeforeCap_LateDayDistantTrip_DoesNotFit()
    {
        // 7:55pm, a 60-tile round trip can't complete before 8pm.
        Assert.False(ShiftClockEstimator.FitsBeforeCap(
            timeOfDay: 1955, hardCapTimeOfDay: 2000,
            outboundTiles: 60, homeboundTiles: 60, walkPixelsPerTick: 2f, workHeadroomMinutes: 10));
    }

    [Fact]
    public void FitsBeforeCap_DegenerateSpeed_DoesNotFit()
    {
        Assert.False(ShiftClockEstimator.FitsBeforeCap(
            timeOfDay: 1000, hardCapTimeOfDay: 2000,
            outboundTiles: 5, homeboundTiles: 5, walkPixelsPerTick: 0f, workHeadroomMinutes: 0));
    }

    [Fact]
    public void FitsBeforeCap_BoundaryLandsExactlyOnCap_Fits()
    {
        // Construct a trip whose estimate lands exactly at the cap (<= is inclusive).
        var walk = 2f;
        var headroom = 4;
        var needed = ShiftClockEstimator.EstimateWalkMinutes(10, walk) * 2 + headroom; // 8 + 8 + 4 = 20
        // 2000 == 20:00 == 1200 absolute minutes; start exactly `needed` minutes earlier (=1940).
        var startAbs = 20 * 60 - needed;
        var start = startAbs / 60 * 100 + startAbs % 60;

        Assert.True(ShiftClockEstimator.FitsBeforeCap(start, 2000, 10, 10, walk, headroom));
        // One 10-minute step later must NOT fit.
        Assert.False(ShiftClockEstimator.FitsBeforeCap(
            ShiftClockEstimator.AddInGameMinutes(start, 10), 2000, 10, 10, walk, headroom));
    }
}
