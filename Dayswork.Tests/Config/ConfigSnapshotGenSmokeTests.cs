namespace Dayswork.Tests.Config;

using Dayswork.Core.Config;
using Dayswork.Core.Domain;
using Dayswork.Tests.Generators;
using FsCheck;
using FsCheck.Xunit;

public class ConfigSnapshotGenSmokeTests
{
    [Property(Arbitrary = new[] { typeof(ConfigSnapshotGen) })]
    public bool Generated_snapshots_satisfy_all_INV_CFG_invariants(IConfigSnapshot s)
    {
        var allKinds = Enum.GetValues<TaskKind>();

        return s.BaseRate >= 0
            && s.TaskIncrements.Values.All(v => v >= 0)
            && s.TaskIncrements.Count == allKinds.Length
            && allKinds.All(k => s.TaskIncrements.ContainsKey(k))
            && s.AverageSpeedConstant > 0
            && s.HardCapTime >= 1000
            && s.HardCapTime <= 2600
            && s.StuckInitialWaitMinutes >= 1
            && s.StuckPostTeleportWaitMinutes >= 1;
    }
}
