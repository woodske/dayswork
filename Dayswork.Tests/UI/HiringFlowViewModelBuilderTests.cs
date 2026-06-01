namespace Dayswork.Tests.UI;

using Dayswork.Core.Config;
using Dayswork.Core.Domain;
using Dayswork.Tests.Pricing;
using Dayswork.UI;
using Xunit;

public sealed class HiringFlowViewModelBuilderTests
{
    private readonly IConfigSnapshot _config = ConfigDefaults.Build();

    [Fact]
    public void Build_NoOutdoorScopeForOutdoorTask_StaysInvalid()
    {
        var draft = CreateDraft(
            new ContractScopeSelection(
                OutdoorZones: Array.Empty<Zone>(),
                AnimalBuildings: Array.Empty<AnimalBuildingSelection>(),
                Greenhouse: null),
            new HashSet<TaskKind> { TaskKind.HarvestCrops });

        var preview = U18BuilderFactory.CreateTermsBuilder().BuildPreview(
            draft.ScopeSelection,
            draft.EnabledTasks,
            _config);

        var state = HiringFlowViewModelBuilder.Build(draft, preview);

        Assert.False(state.ReviewModel.CanConfirm);
        var row = Assert.Single(state.ServiceRows);
        Assert.Equal(TaskKind.HarvestCrops, row.Service);
        Assert.Equal(ServiceContributionState.NeedsOutdoorScope, row.RowState);
    }

    [Fact]
    public void Build_GreenhouseOnlyWaterTask_IsChargedAndConfirmable()
    {
        var draft = CreateDraft(
            new ContractScopeSelection(
                OutdoorZones: Array.Empty<Zone>(),
                AnimalBuildings: Array.Empty<AnimalBuildingSelection>(),
                Greenhouse: new GreenhouseSelection("Greenhouse")),
            new HashSet<TaskKind> { TaskKind.WaterCrops });

        var preview = U18BuilderFactory.CreateTermsBuilder().BuildPreview(
            draft.ScopeSelection,
            draft.EnabledTasks,
            _config);

        var state = HiringFlowViewModelBuilder.Build(draft, preview);

        Assert.True(state.ReviewModel.CanConfirm);
        var row = Assert.Single(state.ServiceRows);
        Assert.Equal(ServiceContributionState.Charged, row.RowState);
        Assert.NotNull(row.DisplayAmount);
        Assert.NotNull(state.ReviewModel.Pricing);
        Assert.NotNull(state.ReviewModel.WorkerEnergy);
    }

    [Fact]
    public void Build_RecurringEdit_UsesRecurringEditPaymentTiming()
    {
        var draft = CreateDraft(
            new ContractScopeSelection(
                OutdoorZones: new[] { new Zone("Farm", new TileCoord(0, 0), new TileCoord(3, 3)) },
                AnimalBuildings: Array.Empty<AnimalBuildingSelection>(),
                Greenhouse: null),
            new HashSet<TaskKind> { TaskKind.ClearRocks },
            schedule: ContractSchedule.Recurring,
            editingId: ContractId.New());

        var preview = U18BuilderFactory.CreateTermsBuilder().BuildPreview(
            draft.ScopeSelection,
            draft.EnabledTasks,
            _config);

        var state = HiringFlowViewModelBuilder.Build(draft, preview);

        Assert.Equal(PaymentTimingKind.RecurringEditAppliesNextEligibleDay, state.ReviewModel.PaymentTimingKind);
    }

    [Fact]
    public void LegacyScopeBootstrapper_BootstrapsOutdoorAnimalAndGreenhouseScope()
    {
        var zones = new[]
        {
            new Zone("Farm", new TileCoord(1, 1), new TileCoord(4, 4)),
            new Zone("Big Coop", new TileCoord(0, 0), new TileCoord(999, 999)),
            new Zone("Greenhouse", new TileCoord(0, 0), new TileCoord(999, 999)),
        };

        var selection = LegacyScopeBootstrapper.Bootstrap(zones);

        Assert.Single(selection.OutdoorZones);
        var animal = Assert.Single(selection.AnimalBuildings);
        Assert.Equal(AnimalBuildingTier.BigCoop, animal.Tier);
        Assert.NotNull(selection.Greenhouse);
        Assert.Equal("Greenhouse", selection.Greenhouse!.LocationName);
    }

    [Fact]
    public void LegacyScopeBootstrapper_ProjectCompatibilityZones_RoundTripsTypedScope()
    {
        var selection = new ContractScopeSelection(
            OutdoorZones: new[] { new Zone("Farm", new TileCoord(2, 2), new TileCoord(6, 6)) },
            AnimalBuildings: new[] { new AnimalBuildingSelection("Deluxe Barn", AnimalBuildingTier.DeluxeBarn) },
            Greenhouse: new GreenhouseSelection("Greenhouse"));

        var projected = LegacyScopeBootstrapper.ProjectCompatibilityZones(selection);

        Assert.Contains(projected, zone => zone.LocationName == "Farm");
        Assert.Contains(projected, zone => zone.LocationName == "Deluxe Barn");
        Assert.Contains(projected, zone => zone.LocationName == "Greenhouse");
    }

    private static ContractDraft CreateDraft(
        ContractScopeSelection selection,
        IReadOnlySet<TaskKind> enabledTasks,
        ContractSchedule schedule = ContractSchedule.OneTime,
        ContractId? editingId = null)
    {
        var draft = new ContractDraft
        {
            Schedule = schedule,
            EditingId = editingId,
        };

        draft.EnabledTasks.UnionWith(enabledTasks);
        draft.OutdoorZones.AddRange(selection.OutdoorZones);
        draft.AnimalBuildings.AddRange(selection.AnimalBuildings);
        draft.Greenhouses.AddRange(selection.Greenhouses);
        return draft;
    }
}
