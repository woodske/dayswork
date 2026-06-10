namespace Dayswork.Tests.UI;

using Dayswork.Core.Config;
using Dayswork.Core.Domain;
using Dayswork.Tests.Pricing;
using Dayswork.UI;
using Xunit;

public sealed class HiringFlowViewModelBuilderTests
{
    private readonly ConfigSnapshot _config = ConfigDefaults.Build();

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
            draft.Tier,
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
            draft.Tier,
            _config);

        var state = HiringFlowViewModelBuilder.Build(draft, preview);

        Assert.True(state.ReviewModel.CanConfirm);
        var row = Assert.Single(state.ServiceRows);
        Assert.Equal(ServiceContributionState.Charged, row.RowState);
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
            draft.Tier,
            _config);

        var state = HiringFlowViewModelBuilder.Build(draft, preview);

        Assert.Equal(PaymentTimingKind.RecurringEditAppliesNextEligibleDay, state.ReviewModel.PaymentTimingKind);
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
