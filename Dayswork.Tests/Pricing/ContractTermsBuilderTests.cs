namespace Dayswork.Tests.Pricing;

using Dayswork.Core.Config;
using Dayswork.Core.Domain;
using Dayswork.Core.Pricing;
using Xunit;

public sealed class ContractTermsBuilderTests
{
    private readonly IContractTermsBuilder _builder = U18BuilderFactory.CreateTermsBuilder();
    private readonly IConfigSnapshot _config = ConfigDefaults.Build();

    [Theory]
    [InlineData(EnergyTier.HalfDay)]
    [InlineData(EnergyTier.FullDay)]
    [InlineData(EnergyTier.Overtime)]
    public void BuildPreview_ValidSelection_PricesAndCapacitiesFromTier(EnergyTier tier)
    {
        var preview = _builder.BuildPreview(
            new ContractScopeSelection(
                OutdoorZones: Array.Empty<Zone>(),
                AnimalBuildings: new[] { new AnimalBuildingSelection("Coop A", AnimalBuildingTier.DeluxeCoop) },
                Greenhouse: null),
            new HashSet<TaskKind> { TaskKind.PetAnimals },
            tier,
            _config);

        Assert.True(preview.IsValid);
        Assert.Equal(_config.EnergyTierPrice[tier], preview.ProposedTerms!.Pricing.TotalPrice);
        Assert.Equal(_config.EnergyTierEnergy[tier], preview.ProposedTerms.Energy.DailyCapacity);
    }

    [Fact]
    public void BuildPreview_PriceIsIndependentOfScopeSize()
    {
        var smallScope = new ContractScopeSelection(
            OutdoorZones: new[] { new Zone("Farm", new TileCoord(0, 0), new TileCoord(0, 0)) },
            AnimalBuildings: Array.Empty<AnimalBuildingSelection>(),
            Greenhouse: null);
        var largeScope = new ContractScopeSelection(
            OutdoorZones: new[] { new Zone("Farm", new TileCoord(0, 0), new TileCoord(40, 40)) },
            AnimalBuildings: Array.Empty<AnimalBuildingSelection>(),
            Greenhouse: null);
        var tasks = new HashSet<TaskKind> { TaskKind.WaterCrops };

        var small = _builder.BuildPreview(smallScope, tasks, EnergyTier.FullDay, _config);
        var large = _builder.BuildPreview(largeScope, tasks, EnergyTier.FullDay, _config);

        Assert.Equal(small.ProposedTerms!.Pricing.TotalPrice, large.ProposedTerms!.Pricing.TotalPrice);
    }

    [Fact]
    public void BuildPreview_NoChargeablePairs_IsInvalid()
    {
        var preview = _builder.BuildPreview(
            new ContractScopeSelection(
                OutdoorZones: Array.Empty<Zone>(),
                AnimalBuildings: Array.Empty<AnimalBuildingSelection>(),
                Greenhouse: null),
            new HashSet<TaskKind> { TaskKind.HarvestCrops },
            EnergyTier.FullDay,
            _config);

        Assert.False(preview.IsValid);
        Assert.Null(preview.ProposedTerms);
        Assert.Contains(
            preview.ValidationIssues,
            issue => issue.Code == ContractValidationCode.NoChargeableScopeTaskPair);
    }

    [Fact]
    public void BuildTerms_InvalidSelection_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => _builder.BuildTerms(
            new ContractScopeSelection(
                OutdoorZones: Array.Empty<Zone>(),
                AnimalBuildings: Array.Empty<AnimalBuildingSelection>(),
                Greenhouse: null),
            new HashSet<TaskKind> { TaskKind.ClearRocks },
            EnergyTier.FullDay,
            _config));

        Assert.Contains("no chargeable scope-task pairs", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
