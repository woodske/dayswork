namespace Dayswork.Tests.Pricing;

using Dayswork.Core.Config;
using Dayswork.Core.Crops;
using Dayswork.Core.Domain;
using Dayswork.Core.Machines;
using Dayswork.Core.Pricing;
using Xunit;

public sealed class ContractTermsBuilderTests
{
    private readonly ContractTermsBuilder _builder = ContractTermsBuilderFactory.CreateTermsBuilder();
    private readonly ConfigSnapshot _config = ConfigDefaults.Build();

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
    public void BuildPreview_ManagedCropsOnly_IsValid()
    {
        var crop = new CropDescriptor(
            cropItemId: "(O)24",
            seedItemId: "(O)473",
            fertilizerItemId: null,
            daysToFirstHarvest: 8,
            fertilizedDaysToFirstHarvest: null,
            regrowDays: null,
            seasons: new[] { Season.Spring });
        var assignment = new CropZoneAssignment(
            zone: new Zone("Farm", new TileCoord(0, 0), new TileCoord(5, 5)),
            mode: CropAssignmentMode.Seasonal,
            choices: new[] { new SeasonCropChoice(Season.Spring, crop) });
        var cropPlan = new CropPlan(new[] { assignment });

        var preview = _builder.BuildPreview(
            new ContractScopeSelection(
                OutdoorZones: Array.Empty<Zone>(),
                AnimalBuildings: Array.Empty<AnimalBuildingSelection>(),
                Greenhouse: null),
            new HashSet<TaskKind>(),
            EnergyTier.FullDay,
            _config,
            cropPlan);

        Assert.True(preview.IsValid);
        Assert.NotNull(preview.ProposedTerms);
    }

    private static readonly ContractScopeSelection EmptySelection = new(
        OutdoorZones: Array.Empty<Zone>(),
        AnimalBuildings: Array.Empty<AnimalBuildingSelection>(),
        Greenhouse: null);

    private static MachineWorkScope MachineScope(MachineGroupMode mode, ChestRef? inputChest) =>
        new(new[]
        {
            new MachineGroup(
                "group-a",
                new[] { new MachineRef("Farm", new TileCoord(4, 4), "(BC)16") },
                MachineInputFilter.Any,
                inputChest,
                AutomaticOutputDestination.Instance,
                mode),
        });

    [Fact]
    public void BuildPreview_MachinesOnly_IsValid()
    {
        var preview = _builder.BuildPreview(
            EmptySelection,
            new HashSet<TaskKind>(),
            EnergyTier.FullDay,
            _config,
            cropPlan: null,
            machineScope: MachineScope(MachineGroupMode.CollectOnly, inputChest: null));

        Assert.True(preview.IsValid);
        Assert.NotNull(preview.ProposedTerms);
    }

    [Fact]
    public void BuildPreview_MachinesAddNoSurcharge_PriceEqualsTierPrice()
    {
        var preview = _builder.BuildPreview(
            EmptySelection,
            new HashSet<TaskKind>(),
            EnergyTier.FullDay,
            _config,
            cropPlan: null,
            machineScope: MachineScope(MachineGroupMode.CollectAndReload, new ChestRef("Farm", new TileCoord(1, 1))));

        Assert.Equal(_config.EnergyTierPrice[EnergyTier.FullDay], preview.ProposedTerms!.Pricing.TotalPrice);
    }

    [Fact]
    public void BuildPreview_ReloadGroupWithoutInputChest_FlagsNeedsInputChestButStaysValid()
    {
        var preview = _builder.BuildPreview(
            EmptySelection,
            new HashSet<TaskKind>(),
            EnergyTier.FullDay,
            _config,
            cropPlan: null,
            machineScope: MachineScope(MachineGroupMode.CollectAndReload, inputChest: null));

        Assert.True(preview.IsValid);
        Assert.Contains(
            preview.ValidationIssues,
            issue => issue.Code == ContractValidationCode.MachineGroupNeedsInputChest);
    }

    [Fact]
    public void BuildPreview_CollectOnlyGroupWithoutInputChest_NoNeedsInputChestIssue()
    {
        var preview = _builder.BuildPreview(
            EmptySelection,
            new HashSet<TaskKind>(),
            EnergyTier.FullDay,
            _config,
            cropPlan: null,
            machineScope: MachineScope(MachineGroupMode.CollectOnly, inputChest: null));

        Assert.DoesNotContain(
            preview.ValidationIssues,
            issue => issue.Code == ContractValidationCode.MachineGroupNeedsInputChest);
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
