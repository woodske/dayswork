// PBT-07: centralized FsCheck arbitrary for IConfigSnapshot.
// Used by U-03 smoke PBT and all U-05+ pricing PBTs.
// Generates only invariant-preserving snapshots (all INV-CFG-* satisfied by construction).
namespace Dayswork.Tests.Generators;

using System.Collections.ObjectModel;
using Dayswork.Core.Config;
using Dayswork.Core.Domain;
using Dayswork.Core.Energy;
using FsCheck;

public static class ConfigSnapshotGen
{
    public static Arbitrary<IConfigSnapshot> Snapshot()
    {
        var defaults = (ConfigSnapshot)ConfigDefaults.Build();

        var thresholdGen = Gen.Sequence(
            Enum.GetValues<OutdoorBandSize>().Select(band => Gen.Choose(1, 512).Select(v => (band, v)))
        ).Select(pairs =>
        {
            var raw = pairs.ToDictionary(pair => pair.band, pair => pair.v);
            var small = raw[OutdoorBandSize.Small];
            var medium = Math.Max(small, raw[OutdoorBandSize.Medium]);
            var large = Math.Max(medium, raw[OutdoorBandSize.Large]);

            return (IReadOnlyDictionary<OutdoorBandSize, int>)new ReadOnlyDictionary<OutdoorBandSize, int>(
                new Dictionary<OutdoorBandSize, int>
                {
                    [OutdoorBandSize.Small] = small,
                    [OutdoorBandSize.Medium] = medium,
                    [OutdoorBandSize.Large] = large,
                });
        });

        var outdoorPriceGen = Gen.Sequence(
            defaults.OutdoorServiceBandPrices.Keys
                .Select(key => Gen.Choose(0, 1000).Select(v => (key, v)))
        ).Select(pairs =>
            (IReadOnlyDictionary<OutdoorPriceKey, int>)new ReadOnlyDictionary<OutdoorPriceKey, int>(
                pairs.ToDictionary(pair => pair.key, pair => pair.v)));

        var animalPriceGen = Gen.Sequence(
            defaults.AnimalBuildingPrices.Keys
                .Select(key => Gen.Choose(0, 1000).Select(v => (key, v)))
        ).Select(pairs =>
            (IReadOnlyDictionary<AnimalBuildingPriceKey, int>)new ReadOnlyDictionary<AnimalBuildingPriceKey, int>(
                pairs.ToDictionary(pair => pair.key, pair => pair.v)));

        var greenhousePriceGen = Gen.Sequence(
            defaults.GreenhouseServicePrices.Keys
                .Select(key => Gen.Choose(0, 1000).Select(v => (key, v)))
        ).Select(pairs =>
            (IReadOnlyDictionary<GreenhousePriceKey, int>)new ReadOnlyDictionary<GreenhousePriceKey, int>(
                pairs.ToDictionary(pair => pair.key, pair => pair.v)));

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
            from thresholds in thresholdGen
            from outdoorPrices in outdoorPriceGen
            from animalPrices in animalPriceGen
            from greenhousePrices in greenhousePriceGen
            from energyCapacity in Gen.Choose(1, 500)
            from actionCosts in actionCostGen
            select (IConfigSnapshot)ConfigSnapshotFactory.Create(
                hardCap,
                stuckInit,
                stuckPost,
                walkPixels,
                actionAnimationMs,
                entranceHoldTicks,
                thresholds,
                outdoorPrices,
                animalPrices,
                greenhousePrices,
                energyCapacity,
                actionCosts);

        return snapshotGen.ToArbitrary();
    }
}
