namespace Dayswork.Tests.Pricing;

using Dayswork.Core.Config;
using Dayswork.Core.Domain;
using Dayswork.Core.Pricing;
using Dayswork.Tests.Generators;
using FsCheck;
using FsCheck.Xunit;
using Xunit;

public sealed class DepositHoursPolicyTests
{
    [Fact]
    public void EstimateBillableHours_HugeBuildingPlaceholder_ReturnsFlatPreviewHours()
    {
        var zones = new[]
        {
            new Zone("Barn", new TileCoord(0, 0), new TileCoord(999, 999)),
            new Zone("Coop", new TileCoord(0, 0), new TileCoord(999, 999)),
            new Zone("Shed", new TileCoord(0, 0), new TileCoord(999, 999)),
        };

        var result = DepositHoursPolicy.EstimateBillableHours(
            zones,
            Enum.GetValues<TaskKind>().Length,
            ConfigDefaults.Build());

        Assert.Equal(DepositHoursPolicy.FlatPreviewHours, result);
    }

    [Property(MaxTest = 1000)]
    public Property EstimateBillableHours_IsIndependentOfSavedZoneShape() =>
        Prop.ForAll(
            ConfigSnapshotGen.Snapshot(),
            ZoneGen.ZoneList(),
            Gen.Choose(0, 10).ToArbitrary(),
            (config, zones, tasks) =>
                DepositHoursPolicy.EstimateBillableHours(zones, tasks, config)
                == DepositHoursPolicy.FlatPreviewHours);
}
