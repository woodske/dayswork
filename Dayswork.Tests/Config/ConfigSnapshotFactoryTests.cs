namespace Dayswork.Tests.Config;

using System.Collections.ObjectModel;
using Dayswork.Core.Config;
using Dayswork.Core.Domain;
using Dayswork.Core.Energy;
using Xunit;

public class ConfigSnapshotFactoryTests
{
    private static ConfigSnapshot CreateWith(
        int hardCapTime = 2000,
        float workerWalkPixelsPerTick = 2f,
        IReadOnlyDictionary<EnergyTier, int>? tierEnergy = null,
        IReadOnlyDictionary<EnergyTier, int>? tierPrice = null)
    {
        var defaults = (ConfigSnapshot)ConfigDefaults.Build();
        return ConfigSnapshotFactory.Create(
            hardCapTime: hardCapTime,
            stuckInitialWaitMinutes: 10,
            stuckPostTeleportWaitMinutes: 10,
            workerWalkPixelsPerTick: workerWalkPixelsPerTick,
            workerActionAnimationMs: defaults.WorkerActionAnimationMs,
            workerEntranceHoldTicks: defaults.WorkerEntranceHoldTicks,
            workOnHolidays: defaults.WorkOnHolidays,
            energyTierEnergy: tierEnergy ?? defaults.EnergyTierEnergy,
            energyTierPrice: tierPrice ?? defaults.EnergyTierPrice,
            workActionCosts: defaults.WorkActionCosts);
    }

    [Fact]
    public void Create_returns_snapshot_for_valid_values()
    {
        var defaults = ConfigDefaults.Build();
        var snapshot = CreateWith();

        Assert.Equal(2000, snapshot.HardCapTime);
        Assert.Equal(10, snapshot.StuckInitialWaitMinutes);
        Assert.Equal(10, snapshot.StuckPostTeleportWaitMinutes);
        Assert.Equal(defaults.WorkOnHolidays, snapshot.WorkOnHolidays);
        Assert.Equal(defaults.EnergyTierEnergy[EnergyTier.FullDay], snapshot.EnergyTierEnergy[EnergyTier.FullDay]);
        Assert.Equal(defaults.EnergyTierPrice[EnergyTier.FullDay], snapshot.EnergyTierPrice[EnergyTier.FullDay]);
    }

    [Theory]
    [InlineData(959)]
    [InlineData(2610)]
    public void Create_throws_when_hard_cap_time_is_out_of_range(int invalidHardCapTime)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateWith(hardCapTime: invalidHardCapTime));
    }

    [Fact]
    public void Create_throws_when_a_tier_energy_is_not_positive()
    {
        var invalid = new ReadOnlyDictionary<EnergyTier, int>(new Dictionary<EnergyTier, int>
        {
            [EnergyTier.HalfDay] = 100,
            [EnergyTier.FullDay] = 0,
            [EnergyTier.Overtime] = 300,
        });

        Assert.Throws<ArgumentOutOfRangeException>(() => CreateWith(tierEnergy: invalid));
    }

    [Fact]
    public void Create_throws_when_a_tier_energy_key_is_missing()
    {
        var incomplete = new ReadOnlyDictionary<EnergyTier, int>(new Dictionary<EnergyTier, int>
        {
            [EnergyTier.HalfDay] = 100,
        });

        Assert.Throws<InvalidOperationException>(() => CreateWith(tierEnergy: incomplete));
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    public void Create_throws_when_worker_walk_pixels_per_tick_is_not_positive(float invalidValue)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateWith(workerWalkPixelsPerTick: invalidValue));
    }
}
