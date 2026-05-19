// PBT-07: centralized FsCheck arbitrary for IConfigSnapshot.
// Used by U-03 smoke PBT and all U-05+ pricing PBTs.
// Generates only invariant-preserving snapshots (all INV-CFG-* satisfied by construction).
namespace Dayswork.Tests.Generators;

using System.Collections.ObjectModel;
using Dayswork.Core.Config;
using Dayswork.Core.Domain;
using FsCheck;

public static class ConfigSnapshotGen
{
    public static Arbitrary<IConfigSnapshot> Snapshot()
    {
        var taskKinds = Enum.GetValues<TaskKind>();

        var incrementsGen = Gen.Sequence(
            taskKinds.Select(k => Gen.Choose(0, 200).Select(v => (k, v)))
        ).Select(pairs =>
            (IReadOnlyDictionary<TaskKind, int>)new ReadOnlyDictionary<TaskKind, int>(
                pairs.ToDictionary(p => p.k, p => p.v)
            )
        );

        var snapshotGen =
            from baseRate in Gen.Choose(0, 1000)
            from increments in incrementsGen
            from speed in Gen.Choose(1, 100).Select(x => (double)x)
            from hardCap in Gen.Choose(1000, 2600)
            from stuckInit in Gen.Choose(1, 120)
            from stuckPost in Gen.Choose(1, 120)
            select (IConfigSnapshot)new ConfigSnapshot(
                baseRate, increments, speed, hardCap, stuckInit, stuckPost
            );

        return snapshotGen.ToArbitrary();
    }
}
