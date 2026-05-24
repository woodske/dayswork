namespace Dayswork.Tests.Pricing;

using Dayswork.Core.Config;
using Dayswork.Core.Domain;
using Dayswork.Core.Pricing;
using Xunit;

public sealed class ContractTermsBuilderTests
{
    private readonly IContractTermsBuilder _builder = U18BuilderFactory.CreateTermsBuilder();
    private readonly IConfigSnapshot _config = ConfigDefaults.Build();

    [Fact]
    public void BuildPreview_TwoDeluxeCoops_AggregatesToSingleAnimalLine()
    {
        var preview = _builder.BuildPreview(
            new ContractScopeSelection(
                OutdoorZones: Array.Empty<Zone>(),
                AnimalBuildings: new[]
                {
                    new AnimalBuildingSelection("Coop A", AnimalBuildingTier.DeluxeCoop),
                    new AnimalBuildingSelection("Coop B", AnimalBuildingTier.DeluxeCoop),
                },
                Greenhouse: null),
            new HashSet<TaskKind> { TaskKind.PetAnimals },
            _config);

        Assert.True(preview.IsValid);
        var line = Assert.Single(preview.ProposedTerms!.Pricing.LineItems);
        Assert.Equal(PricingFamily.AnimalBuilding, line.Family);
        Assert.Equal(TaskKind.PetAnimals, line.Service);
        Assert.Equal(2, line.Quantity);
        Assert.Equal(line.UnitPrice * 2, line.LineTotal);
    }

    [Fact]
    public void BuildPreview_GreenhouseWaterAndHarvest_ProducesSeparatePackageLines()
    {
        var preview = _builder.BuildPreview(
            new ContractScopeSelection(
                OutdoorZones: Array.Empty<Zone>(),
                AnimalBuildings: Array.Empty<AnimalBuildingSelection>(),
                Greenhouse: new GreenhouseSelection("Greenhouse")),
            new HashSet<TaskKind> { TaskKind.WaterCrops, TaskKind.HarvestCrops },
            _config);

        Assert.True(preview.IsValid);
        var greenhouseLines = preview.ProposedTerms!.Pricing.LineItems
            .Where(line => line.Family == PricingFamily.Greenhouse)
            .ToList();

        Assert.Equal(2, greenhouseLines.Count);
        Assert.Contains(greenhouseLines, line => line.Service == TaskKind.WaterCrops);
        Assert.Contains(greenhouseLines, line => line.Service == TaskKind.HarvestCrops);
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
            _config));

        Assert.Contains("no chargeable scope-task pairs", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
