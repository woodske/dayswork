namespace Dayswork.Tests.Config;

using Dayswork.Core.Config;
using Dayswork.Core.Domain;
using Dayswork.Core.Energy;
using Xunit;

public class ConfigSnapshotFactoryTests
{
    [Fact]
    public void Create_returns_snapshot_for_valid_values()
    {
        var increments = Enum.GetValues<TaskKind>()
            .ToDictionary(kind => kind, kind => 10);
        var defaults = ConfigDefaults.Build();

        var snapshot = ConfigSnapshotFactory.Create(
            baseRate: 50,
            taskIncrements: increments,
            averageSpeedConstant: 0.3,
            hardCapTime: 2000,
            stuckInitialWaitMinutes: 10,
            stuckPostTeleportWaitMinutes: 10,
            outdoorBandThresholds: defaults.OutdoorBandThresholds,
            outdoorServiceBandPrices: defaults.OutdoorServiceBandPrices,
            animalBuildingPrices: defaults.AnimalBuildingPrices,
            greenhouseServicePrices: defaults.GreenhouseServicePrices,
            workerDailyEnergyCapacity: defaults.WorkerDailyEnergyCapacity,
            workActionCosts: defaults.WorkActionCosts);

        Assert.Equal(50, snapshot.BaseRate);
        Assert.Equal(10, snapshot.TaskIncrements[TaskKind.CutTrees]);
        Assert.Equal(0.3, snapshot.AverageSpeedConstant);
        Assert.Equal(2000, snapshot.HardCapTime);
        Assert.Equal(10, snapshot.StuckInitialWaitMinutes);
        Assert.Equal(10, snapshot.StuckPostTeleportWaitMinutes);
        Assert.Equal(defaults.WorkerDailyEnergyCapacity, snapshot.WorkerDailyEnergyCapacity);
    }

    [Fact]
    public void Create_throws_when_task_increment_is_missing()
    {
        var increments = Enum.GetValues<TaskKind>()
            .Where(kind => kind != TaskKind.ClearGrass)
            .ToDictionary(kind => kind, kind => 10);
        var defaults = ConfigDefaults.Build();

        var ex = Assert.Throws<InvalidOperationException>(() => ConfigSnapshotFactory.Create(
            baseRate: 50,
            taskIncrements: increments,
            averageSpeedConstant: 0.3,
            hardCapTime: 2000,
            stuckInitialWaitMinutes: 10,
            stuckPostTeleportWaitMinutes: 10,
            outdoorBandThresholds: defaults.OutdoorBandThresholds,
            outdoorServiceBandPrices: defaults.OutdoorServiceBandPrices,
            animalBuildingPrices: defaults.AnimalBuildingPrices,
            greenhouseServicePrices: defaults.GreenhouseServicePrices,
            workerDailyEnergyCapacity: defaults.WorkerDailyEnergyCapacity,
            workActionCosts: defaults.WorkActionCosts));

        Assert.Contains(nameof(TaskKind.ClearGrass), ex.Message);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public void Create_throws_when_average_speed_constant_is_not_positive(double invalidValue)
    {
        var increments = Enum.GetValues<TaskKind>()
            .ToDictionary(kind => kind, kind => 10);
        var defaults = ConfigDefaults.Build();

        Assert.Throws<ArgumentOutOfRangeException>(() => ConfigSnapshotFactory.Create(
            baseRate: 50,
            taskIncrements: increments,
            averageSpeedConstant: invalidValue,
            hardCapTime: 2000,
            stuckInitialWaitMinutes: 10,
            stuckPostTeleportWaitMinutes: 10,
            outdoorBandThresholds: defaults.OutdoorBandThresholds,
            outdoorServiceBandPrices: defaults.OutdoorServiceBandPrices,
            animalBuildingPrices: defaults.AnimalBuildingPrices,
            greenhouseServicePrices: defaults.GreenhouseServicePrices,
            workerDailyEnergyCapacity: defaults.WorkerDailyEnergyCapacity,
            workActionCosts: defaults.WorkActionCosts));
    }

    [Theory]
    [InlineData(959)]
    [InlineData(2610)]
    public void Create_throws_when_hard_cap_time_is_out_of_range(int invalidHardCapTime)
    {
        var increments = Enum.GetValues<TaskKind>()
            .ToDictionary(kind => kind, kind => 10);
        var defaults = ConfigDefaults.Build();

        Assert.Throws<ArgumentOutOfRangeException>(() => ConfigSnapshotFactory.Create(
            baseRate: 50,
            taskIncrements: increments,
            averageSpeedConstant: 0.3,
            hardCapTime: invalidHardCapTime,
            stuckInitialWaitMinutes: 10,
            stuckPostTeleportWaitMinutes: 10,
            outdoorBandThresholds: defaults.OutdoorBandThresholds,
            outdoorServiceBandPrices: defaults.OutdoorServiceBandPrices,
            animalBuildingPrices: defaults.AnimalBuildingPrices,
            greenhouseServicePrices: defaults.GreenhouseServicePrices,
            workerDailyEnergyCapacity: defaults.WorkerDailyEnergyCapacity,
            workActionCosts: defaults.WorkActionCosts));
    }

    [Fact]
    public void Create_throws_when_energy_capacity_is_not_positive()
    {
        var increments = Enum.GetValues<TaskKind>()
            .ToDictionary(kind => kind, kind => 10);
        var defaults = ConfigDefaults.Build();

        Assert.Throws<ArgumentOutOfRangeException>(() => ConfigSnapshotFactory.Create(
            baseRate: 50,
            taskIncrements: increments,
            averageSpeedConstant: 0.3,
            hardCapTime: 2000,
            stuckInitialWaitMinutes: 10,
            stuckPostTeleportWaitMinutes: 10,
            outdoorBandThresholds: defaults.OutdoorBandThresholds,
            outdoorServiceBandPrices: defaults.OutdoorServiceBandPrices,
            animalBuildingPrices: defaults.AnimalBuildingPrices,
            greenhouseServicePrices: defaults.GreenhouseServicePrices,
            workerDailyEnergyCapacity: 0,
            workActionCosts: defaults.WorkActionCosts));
    }
}
