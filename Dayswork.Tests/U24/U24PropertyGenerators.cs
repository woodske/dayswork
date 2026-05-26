namespace Dayswork.Tests.U24;

using Dayswork.Core.Domain;
using Dayswork.Core.Energy;
using Dayswork.Integration;
using FsCheck;

public static class U24PropertyGenerators
{
    public static Arbitrary<ModConfig> RedesignConfig()
    {
        var smallKey = ContractTermsConfigKeyCodec.EncodeOutdoorBandKey(OutdoorBandSize.Small);
        var mediumKey = ContractTermsConfigKeyCodec.EncodeOutdoorBandKey(OutdoorBandSize.Medium);
        var largeKey = ContractTermsConfigKeyCodec.EncodeOutdoorBandKey(OutdoorBandSize.Large);
        var defaults = ModConfig.CreateDefaults();

        var outdoorPriceGen = Gen.Sequence(
            defaults.OutdoorServiceBandPrices.Keys
                .Select(key => Gen.Choose(0, 2000).Select(value => (key, value))))
            .Select(pairs => pairs.ToDictionary(pair => pair.key, pair => pair.value));

        var animalPriceGen = Gen.Sequence(
            defaults.AnimalBuildingPrices.Keys
                .Select(key => Gen.Choose(0, 2000).Select(value => (key, value))))
            .Select(pairs => pairs.ToDictionary(pair => pair.key, pair => pair.value));

        var greenhousePriceGen = Gen.Sequence(
            defaults.GreenhouseServicePrices.Keys
                .Select(key => Gen.Choose(0, 2000).Select(value => (key, value))))
            .Select(pairs => pairs.ToDictionary(pair => pair.key, pair => pair.value));

        var workActionCostGen = Gen.Sequence(
            defaults.WorkActionCosts.Keys
                .Select(key => Gen.Choose(0, 20).Select(value => (key, value))))
            .Select(pairs => pairs.ToDictionary(pair => pair.key, pair => pair.value));

        var generator =
            from hardCapTime in Gen.Choose(1000, 2600)
            from stuckInitialWait in Gen.Choose(1, 120)
            from stuckPostTeleportWait in Gen.Choose(1, 120)
            from workerWalkPixelsPerTick in Gen.Choose(5, 60).Select(value => value / 10f)
            from workerActionAnimationMs in Gen.Choose(1, 2000)
            from workerEntranceHoldTicks in Gen.Choose(0, 600)
            from workerDailyEnergyCapacity in Gen.Choose(1, 1000)
            from smallThreshold in Gen.Choose(1, 128)
            from mediumThreshold in Gen.Choose(smallThreshold, 256)
            from largeThreshold in Gen.Choose(mediumThreshold, 512)
            from outdoorPrices in outdoorPriceGen
            from animalPrices in animalPriceGen
            from greenhousePrices in greenhousePriceGen
            from workActionCosts in workActionCostGen
            select new ModConfig
            {
                HardCapTime = hardCapTime,
                StuckInitialWaitMinutes = stuckInitialWait,
                StuckPostTeleportWaitMinutes = stuckPostTeleportWait,
                WorkerWalkPixelsPerTick = workerWalkPixelsPerTick,
                WorkerActionAnimationMs = workerActionAnimationMs,
                WorkerEntranceHoldTicks = workerEntranceHoldTicks,
                WorkerDailyEnergyCapacity = workerDailyEnergyCapacity,
                OutdoorBandThresholds = new Dictionary<string, int>
                {
                    [smallKey] = smallThreshold,
                    [mediumKey] = mediumThreshold,
                    [largeKey] = largeThreshold,
                },
                OutdoorServiceBandPrices = outdoorPrices,
                AnimalBuildingPrices = animalPrices,
                GreenhouseServicePrices = greenhousePrices,
                WorkActionCosts = workActionCosts,
            };

        return generator.ToArbitrary();
    }
}
