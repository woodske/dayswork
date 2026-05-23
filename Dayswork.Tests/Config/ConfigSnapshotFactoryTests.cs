namespace Dayswork.Tests.Config;

using Dayswork.Core.Config;
using Dayswork.Core.Domain;
using Xunit;

public class ConfigSnapshotFactoryTests
{
    [Fact]
    public void Create_returns_snapshot_for_valid_values()
    {
        var increments = Enum.GetValues<TaskKind>()
            .ToDictionary(kind => kind, kind => 10);

        var snapshot = ConfigSnapshotFactory.Create(
            baseRate: 50,
            taskIncrements: increments,
            averageSpeedConstant: 0.3,
            hardCapTime: 2000,
            stuckInitialWaitMinutes: 10,
            stuckPostTeleportWaitMinutes: 10);

        Assert.Equal(50, snapshot.BaseRate);
        Assert.Equal(10, snapshot.TaskIncrements[TaskKind.CutTrees]);
        Assert.Equal(0.3, snapshot.AverageSpeedConstant);
        Assert.Equal(2000, snapshot.HardCapTime);
        Assert.Equal(10, snapshot.StuckInitialWaitMinutes);
        Assert.Equal(10, snapshot.StuckPostTeleportWaitMinutes);
    }

    [Fact]
    public void Create_throws_when_task_increment_is_missing()
    {
        var increments = Enum.GetValues<TaskKind>()
            .Where(kind => kind != TaskKind.ClearGrass)
            .ToDictionary(kind => kind, kind => 10);

        var ex = Assert.Throws<InvalidOperationException>(() => ConfigSnapshotFactory.Create(
            baseRate: 50,
            taskIncrements: increments,
            averageSpeedConstant: 0.3,
            hardCapTime: 2000,
            stuckInitialWaitMinutes: 10,
            stuckPostTeleportWaitMinutes: 10));

        Assert.Contains(nameof(TaskKind.ClearGrass), ex.Message);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public void Create_throws_when_average_speed_constant_is_not_positive(double invalidValue)
    {
        var increments = Enum.GetValues<TaskKind>()
            .ToDictionary(kind => kind, kind => 10);

        Assert.Throws<ArgumentOutOfRangeException>(() => ConfigSnapshotFactory.Create(
            baseRate: 50,
            taskIncrements: increments,
            averageSpeedConstant: invalidValue,
            hardCapTime: 2000,
            stuckInitialWaitMinutes: 10,
            stuckPostTeleportWaitMinutes: 10));
    }

    [Theory]
    [InlineData(959)]
    [InlineData(2610)]
    public void Create_throws_when_hard_cap_time_is_out_of_range(int invalidHardCapTime)
    {
        var increments = Enum.GetValues<TaskKind>()
            .ToDictionary(kind => kind, kind => 10);

        Assert.Throws<ArgumentOutOfRangeException>(() => ConfigSnapshotFactory.Create(
            baseRate: 50,
            taskIncrements: increments,
            averageSpeedConstant: 0.3,
            hardCapTime: invalidHardCapTime,
            stuckInitialWaitMinutes: 10,
            stuckPostTeleportWaitMinutes: 10));
    }
}
