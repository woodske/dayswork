namespace Dayswork.Tests.UI;

using Dayswork.Core.Config;
using Dayswork.Core.Domain;
using Dayswork.Tests.Generators;
using Dayswork.Tests.Pricing;
using Dayswork.UI;
using FsCheck;
using FsCheck.Xunit;

public sealed class HiringFlowViewModelPropertyTests
{
    [Property(MaxTest = 300)]
    public Property BuildState_IsDeterministicForEquivalentOutdoorSelections() =>
        Prop.ForAll(
            U18ContractTermsGen.EquivalentOutdoorSelections(),
            ConfigSnapshotGen.Snapshot(),
            (equivalentSelections, config) =>
            {
                var enabledTasks = new HashSet<TaskKind>
                {
                    TaskKind.HarvestCrops,
                    TaskKind.ClearRocks,
                    TaskKind.ClearWeeds,
                };

                var left = BuildState(equivalentSelections.Left, enabledTasks, config, ContractSchedule.OneTime, null);
                var right = BuildState(equivalentSelections.Right, enabledTasks, config, ContractSchedule.OneTime, null);
                return DescribeState(left) == DescribeState(right);
            });

    [Property(MaxTest = 300)]
    public Property ScheduleChange_DoesNotChangePricingOrEnergy() =>
        Prop.ForAll(
            U18ContractTermsGen.ScopeSelection(),
            U18ContractTermsGen.EnabledTaskSet(),
            ConfigSnapshotGen.Snapshot(),
            (selection, enabledTasks, config) =>
            {
                var preview = U18BuilderFactory.CreateTermsBuilder().BuildPreview(selection, enabledTasks, Dayswork.Core.Domain.EnergyTier.FullDay, config);
                if (!preview.IsValid || preview.ProposedTerms is null)
                    return true;

                var oneTime = BuildState(selection, enabledTasks, config, ContractSchedule.OneTime, null);
                var recurring = BuildState(selection, enabledTasks, config, ContractSchedule.Recurring, null);

                return DescribePricingAndEnergy(oneTime) == DescribePricingAndEnergy(recurring);
            });

    [Property(MaxTest = 300)]
    public Property DestinationChanges_DoNotChangePricingOrEnergy() =>
        Prop.ForAll(
            U18ContractTermsGen.ScopeSelection(),
            U18ContractTermsGen.EnabledTaskSet(),
            ConfigSnapshotGen.Snapshot(),
            (selection, enabledTasks, config) =>
            {
                var preview = U18BuilderFactory.CreateTermsBuilder().BuildPreview(selection, enabledTasks, Dayswork.Core.Domain.EnergyTier.FullDay, config);
                if (!preview.IsValid || preview.ProposedTerms is null)
                    return true;

                var withoutDestinations = CreateDraft(selection, enabledTasks);
                var withDestinations = CreateDraft(selection, enabledTasks);
                foreach (var task in enabledTasks.Where(IsOutputTask))
                    withDestinations.Destinations[task] = MailDestination.Instance;

                var left = HiringFlowViewModelBuilder.Build(withoutDestinations, preview);
                var right = HiringFlowViewModelBuilder.Build(withDestinations, preview);

                return DescribePricingAndEnergy(left) == DescribePricingAndEnergy(right);
            });

    private static DraftPreviewState BuildState(
        ContractScopeSelection selection,
        IReadOnlySet<TaskKind> enabledTasks,
        IConfigSnapshot config,
        ContractSchedule schedule,
        ContractId? editingId)
    {
        var draft = CreateDraft(selection, enabledTasks, schedule, editingId);
        var preview = U18BuilderFactory.CreateTermsBuilder().BuildPreview(selection, enabledTasks, Dayswork.Core.Domain.EnergyTier.FullDay, config);
        return HiringFlowViewModelBuilder.Build(draft, preview);
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

    private static bool IsOutputTask(TaskKind task) => task switch
    {
        TaskKind.HarvestCrops => true,
        TaskKind.CollectFruit => true,
        TaskKind.CollectAnimalProducts => true,
        TaskKind.CutTrees => true,
        TaskKind.ClearRocks => true,
        TaskKind.ClearWeeds => true,
        _ => false,
    };

    private static string DescribeState(DraftPreviewState state)
    {
        var rows = string.Join(
            ";",
            state.ServiceRows.Select(row => $"{row.Service}:{row.RowState}"));
        var validation = string.Join(
            ";",
            state.ReviewModel.ValidationMessages.Select(message => $"{message.Code}:{message.RelatedTask?.ToString() ?? "none"}"));

        return $"{DescribePricingAndEnergy(state)}|{rows}|{validation}|{state.ReviewModel.PaymentTimingKind}";
    }

    private static string DescribePricingAndEnergy(DraftPreviewState state)
    {
        var pricing = state.ReviewModel.Pricing is null
            ? "none"
            : $"{state.ReviewModel.Pricing.TotalPrice}";
        var energy = state.ReviewModel.WorkerEnergy is null
            ? "none"
            : $"{state.ReviewModel.WorkerEnergy.DailyCapacity}:{string.Join(",", state.ReviewModel.WorkerEnergy.ActionCosts.OrderBy(kvp => kvp.Key).Select(kvp => $"{kvp.Key}:{kvp.Value}"))}";

        return $"{pricing}|{energy}";
    }
}
