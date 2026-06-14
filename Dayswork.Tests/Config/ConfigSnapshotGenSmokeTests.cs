namespace Dayswork.Tests.Config;

using Dayswork.Core.Config;
using Dayswork.Core.Domain;
using Dayswork.Core.Energy;
using Dayswork.Tests.Generators;
using FsCheck.Xunit;

public class ConfigSnapshotGenSmokeTests
{
    [Property(Arbitrary = new[] { typeof(ConfigSnapshotGen) })]
    public bool Generated_snapshots_satisfy_all_INV_CFG_invariants(ConfigSnapshot s)
    {
        return s.HardCapTime >= 1000
            && s.HardCapTime <= 2600
            && s.StuckInitialWaitMinutes >= 1
            && s.StuckPostTeleportWaitMinutes >= 1
            && s.WorkerWalkPixelsPerTick > 0
            && s.WorkerActionAnimationMs >= 1
            && s.WorkerEntranceHoldTicks >= 0
            && (s.WorkOnHolidays || !s.WorkOnHolidays)
            && s.EnergyTierEnergy.Count == Enum.GetValues<EnergyTier>().Length
            && s.EnergyTierEnergy.Values.All(v => v > 0)
            && s.EnergyTierPrice.Count == Enum.GetValues<EnergyTier>().Length
            && s.EnergyTierPrice.Values.All(v => v >= 0)
            && s.WorkActionCosts.Count == Enum.GetValues<WorkActionKind>().Length
            && s.WorkActionCosts.Values.All(v => v >= 0);
    }
}
