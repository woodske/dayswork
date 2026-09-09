namespace Dayswork.Tests.Net;

using Dayswork.Core.Config;
using Dayswork.Core.Domain;
using Dayswork.Core.Energy;
using Dayswork.Core.Net;
using Dayswork.Core.Pricing;
using Dayswork.Tests.Pricing;
using Xunit;

/// <summary>
/// R9: the bargain a client shows the player has to be the bargain the host commits. The client
/// prices against the host's numbers rather than its own config file, and terms that still
/// disagree are requoted instead of silently substituted at the till.
/// </summary>
public class HostQuotedTermsTests
{
    private static readonly ContractTermsBuilder Builder = ContractTermsBuilderFactory.CreateTermsBuilder();

    private static ConfigSnapshot WithFullDayPrice(ConfigSnapshot config, int price) =>
        ConfigSnapshotFactory.Create(
            config.HardCapTime,
            config.StuckInitialWaitMinutes,
            config.StuckPostTeleportWaitMinutes,
            config.WorkerWalkPixelsPerTick,
            config.WorkerActionAnimationMs,
            config.WorkerEntranceHoldTicks,
            config.WorkOnHolidays,
            config.EagerChestDeposits,
            config.EnergyTierEnergy,
            new Dictionary<EnergyTier, int>(config.EnergyTierPrice) { [EnergyTier.FullDay] = price },
            config.WorkActionCosts);

    private static ContractTermsSnapshot Terms(ConfigSnapshot config) =>
        Builder.BuildTerms(
            new ContractScopeSelection(
                OutdoorZones: new[] { new Zone("Farm", new TileCoord(0, 0), new TileCoord(4, 4)) },
                AnimalBuildings: Array.Empty<AnimalBuildingSelection>(),
                Greenhouse: null),
            new HashSet<TaskKind> { TaskKind.WaterCrops },
            EnergyTier.FullDay,
            config);

    [Fact]
    public void Dayswork2Review_R9_AClientPricingAgainstTheHostsTablesQuotesWhatTheHostCommits()
    {
        // The host charges 1000g for the tier; this client's own config file says the default.
        var hostConfig = WithFullDayPrice(ConfigDefaults.Build(), 1000);
        var clientLocalConfig = ConfigDefaults.Build();

        var quotedFromOwnConfig = Terms(clientLocalConfig);
        var quotedFromHostTables = Terms(
            PricingConfigTransfer.ApplyTo(clientLocalConfig, PricingConfigTransfer.From(hostConfig)));

        Assert.NotEqual(1000, quotedFromOwnConfig.Pricing.TotalPrice);
        Assert.Equal(1000, quotedFromHostTables.Pricing.TotalPrice);
        Assert.True(ContractTermsSnapshot.SameTerms(quotedFromHostTables, Terms(hostConfig)));
        Assert.False(ContractTermsSnapshot.SameTerms(quotedFromOwnConfig, Terms(hostConfig)));
    }

    [Fact]
    public void Dayswork2Review_R9_ADisagreeingEnergyAllowanceIsAMismatchToo()
    {
        var host = ConfigDefaults.Build();
        var raised = ConfigSnapshotFactory.Create(
            host.HardCapTime,
            host.StuckInitialWaitMinutes,
            host.StuckPostTeleportWaitMinutes,
            host.WorkerWalkPixelsPerTick,
            host.WorkerActionAnimationMs,
            host.WorkerEntranceHoldTicks,
            host.WorkOnHolidays,
            host.EagerChestDeposits,
            new Dictionary<EnergyTier, int>(host.EnergyTierEnergy)
            {
                [EnergyTier.FullDay] = host.EnergyTierEnergy[EnergyTier.FullDay] + 50,
            },
            host.EnergyTierPrice,
            host.WorkActionCosts);

        // Same price, different allowance: still not the deal the player reviewed.
        Assert.Equal(Terms(host).Pricing.TotalPrice, Terms(raised).Pricing.TotalPrice);
        Assert.False(ContractTermsSnapshot.SameTerms(Terms(host), Terms(raised)));
    }

    [Fact]
    public void Dayswork2Review_R9_TermsBuiltTwiceFromOneConfigAreTheSameBargain()
    {
        // Record equality cannot say this — the action costs are an IReadOnlyDictionary, compared
        // by reference — so a naive check would requote every commit forever.
        var config = ConfigDefaults.Build();

        Assert.NotEqual(Terms(config), Terms(config));
        Assert.True(ContractTermsSnapshot.SameTerms(Terms(config), Terms(config)));
    }

    [Fact]
    public void Dayswork2Review_R9_APartialPricingTableKeepsTheLocalValuesItDoesNotMention()
    {
        var local = ConfigDefaults.Build();
        var partial = new SnapshotPricingConfig
        {
            EnergyTierPrice = new Dictionary<EnergyTier, int> { [EnergyTier.FullDay] = 1234 },
        };

        var merged = PricingConfigTransfer.ApplyTo(local, partial);

        Assert.Equal(1234, merged.EnergyTierPrice[EnergyTier.FullDay]);
        Assert.Equal(local.EnergyTierEnergy[EnergyTier.FullDay], merged.EnergyTierEnergy[EnergyTier.FullDay]);
        Assert.Equal(local.WorkActionCosts.Count, merged.WorkActionCosts.Count);
    }

    [Fact]
    public void Dayswork2Review_R9_NoHostPricingLeavesTheLocalSnapshotAlone() =>
        Assert.Same(Cached, PricingConfigTransfer.ApplyTo(Cached, null));

    private static readonly ConfigSnapshot Cached = ConfigDefaults.Build();
}
