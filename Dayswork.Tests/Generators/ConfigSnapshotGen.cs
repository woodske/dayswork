// Centralized FsCheck arbitrary for ConfigSnapshot.
// Generates only invariant-preserving snapshots (all INV-CFG-* satisfied by construction).
namespace Dayswork.Tests.Generators;

using System.Collections.ObjectModel;
using Dayswork.Core.Config;
using Dayswork.Core.Domain;
using Dayswork.Core.Energy;
using FsCheck;

public static class ConfigSnapshotGen
{
    public static Arbitrary<ConfigSnapshot> Snapshot()
    {
        var tierEnergyGen = Gen.Sequence(
            Enum.GetValues<EnergyTier>().Select(tier => Gen.Choose(1, 500).Select(v => (tier, v)))
        ).Select(pairs =>
            (IReadOnlyDictionary<EnergyTier, int>)new ReadOnlyDictionary<EnergyTier, int>(
                pairs.ToDictionary(pair => pair.tier, pair => pair.v)));

        var tierPriceGen = Gen.Sequence(
            Enum.GetValues<EnergyTier>().Select(tier => Gen.Choose(0, 5000).Select(v => (tier, v)))
        ).Select(pairs =>
            (IReadOnlyDictionary<EnergyTier, int>)new ReadOnlyDictionary<EnergyTier, int>(
                pairs.ToDictionary(pair => pair.tier, pair => pair.v)));

        var actionCostGen = Gen.Sequence(
            Enum.GetValues<WorkActionKind>().Select(action => Gen.Choose(0, 20).Select(v => (action, v)))
        ).Select(pairs =>
            (IReadOnlyDictionary<WorkActionKind, int>)new ReadOnlyDictionary<WorkActionKind, int>(
                pairs.ToDictionary(pair => pair.action, pair => pair.v)));

        var snapshotGen =
            from hardCap in Gen.Choose(1000, 2600)
            from stuckInit in Gen.Choose(1, 120)
            from stuckPost in Gen.Choose(1, 120)
            from walkPixels in Gen.Choose(1, 6).Select(x => (float)x)
            from actionAnimationMs in Gen.Choose(100, 1500)
            from entranceHoldTicks in Gen.Choose(0, 300)
            from workOnHolidays in Arb.Generate<bool>()
            from tierEnergy in tierEnergyGen
            from tierPrice in tierPriceGen
            from actionCosts in actionCostGen
            select (ConfigSnapshot)ConfigSnapshotFactory.Create(
                hardCap,
                stuckInit,
                stuckPost,
                walkPixels,
                actionAnimationMs,
                entranceHoldTicks,
                workOnHolidays,
                tierEnergy,
                tierPrice,
                actionCosts);

        return snapshotGen.ToArbitrary();
    }
}
