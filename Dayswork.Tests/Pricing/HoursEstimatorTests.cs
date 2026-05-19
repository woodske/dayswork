namespace Dayswork.Tests.Pricing;

using Dayswork.Core.Config;
using Dayswork.Core.Domain;
using Dayswork.Core.Pricing;
using Dayswork.Tests.Generators;
using FsCheck;
using FsCheck.Xunit;
using Xunit;

public class HoursEstimatorTests
{
    private readonly HoursEstimator  _est = new();
    private readonly IConfigSnapshot _cfg = ConfigDefaults.Build();

    // ── [Fact] ───────────────────────────────────────────────────────────────

    [Fact]
    public void EmptyZones_ReturnsZero()
    {
        Assert.Equal(0.0, _est.Estimate(Array.Empty<Zone>(), 3, _cfg));
    }

    [Fact]
    public void ZeroTasks_ReturnsZero()
    {
        var zone = new Zone("Farm", new TileCoord(0, 0), new TileCoord(9, 9));
        Assert.Equal(0.0, _est.Estimate(new[] { zone }, 0, _cfg));
    }

    [Fact]
    public void SingleZone_SingleTask_MatchesFormula()
    {
        // 10×10 = 100 tiles, 1 task, AverageSpeedConstant=0.3 → 100*1*0.3/60 = 0.5
        var zone   = new Zone("Farm", new TileCoord(0, 0), new TileCoord(9, 9));
        var result = _est.Estimate(new[] { zone }, 1, _cfg);
        Assert.Equal(0.5, result, precision: 10);
    }

    [Fact]
    public void TwoZones_AreasAddUp()
    {
        var z1 = new Zone("Farm", new TileCoord(0, 0), new TileCoord(4, 4));   // 5×5 = 25 tiles
        var z2 = new Zone("Farm", new TileCoord(0, 0), new TileCoord(9, 9));   // 10×10 = 100 tiles
        var combined   = _est.Estimate(new[] { z1, z2 }, 1, _cfg);
        var individual = _est.Estimate(new[] { z1 }, 1, _cfg)
                       + _est.Estimate(new[] { z2 }, 1, _cfg);
        Assert.Equal(individual, combined, precision: 10);
    }

    // ── [Property] — PBT-03 ─────────────────────────────────────────────────

    [Property(MaxTest = 1000)]
    public Property Estimate_AlwaysNonNegative() =>
        Prop.ForAll(
            ConfigSnapshotGen.Snapshot(),
            ZoneGen.ZoneList(),
            Gen.Choose(0, 10).ToArbitrary(),
            (config, zones, tasks) =>
                _est.Estimate(zones, tasks, config) >= 0.0);

    [Property(MaxTest = 1000)]
    public Property Estimate_NonDecreasingWithMoreZones() =>
        // Combine (extra zone, numTasks) into a single arbitrary to stay within ForAll's 3-arg limit.
        Prop.ForAll(
            ConfigSnapshotGen.Snapshot(),
            ZoneGen.ZoneList(),
            (from extra in ZoneGen.Zone().Generator
             from n     in Gen.Choose(1, 10)
             select (extra, n)).ToArbitrary(),
            (config, zones, pair) =>
            {
                var (extra, tasks) = pair;
                var before = _est.Estimate(zones, tasks, config);
                var after  = _est.Estimate(zones.Append(extra).ToList(), tasks, config);
                return after >= before;
            });

    [Property(MaxTest = 1000)]
    public Property Estimate_NonDecreasingWithMoreTasks() =>
        Prop.ForAll(
            ConfigSnapshotGen.Snapshot(),
            ZoneGen.ZoneList(),
            Gen.Choose(0, 9).ToArbitrary(),
            (config, zones, tasks) =>
                _est.Estimate(zones, tasks + 1, config) >= _est.Estimate(zones, tasks, config));
}
