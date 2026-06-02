using Dayswork.Core.Domain;

namespace Dayswork.UI;

internal static class HiringFlowViewModelBuilder
{
    public static DraftPreviewState Build(ContractDraft draft, ContractPreview preview)
    {
        var serviceRows = TaskPresentation.TaskOrder
            .Where(draft.EnabledTasks.Contains)
            .Select(task => BuildServiceRow(draft, task))
            .ToList()
            .AsReadOnly();

        var scopeSummary = new ScopeSummaryModel(
            draft.ScopeSelection.OutdoorZones,
            draft.ScopeSelection.AnimalBuildings,
            draft.ScopeSelection.Greenhouses);

        var selectedTasks = TaskPresentation.TaskOrder
            .Where(draft.EnabledTasks.Contains)
            .ToList()
            .AsReadOnly();

        var validationMessages = preview.ValidationIssues
            .OrderBy(issue => issue.Code)
            .ThenBy(issue => issue.RelatedTask)
            .Select(issue => new ValidationDisplayMessage(issue.Code, issue.RelatedTask))
            .ToList()
            .AsReadOnly();

        var reviewModel = new SummaryReviewModel(
            SelectedTasks: selectedTasks,
            ScopeSummary: scopeSummary,
            Pricing: preview.ProposedTerms?.Pricing,
            WorkerEnergy: preview.ProposedTerms?.Energy,
            Tier: draft.Tier,
            CategoryPriority: draft.CategoryPriority.ToList().AsReadOnly(),
            PaymentTimingKind: DeterminePaymentTiming(draft),
            ValidationMessages: validationMessages,
            CanConfirm: preview.IsValid);

        return new DraftPreviewState(
            Preview: preview,
            ServiceRows: serviceRows,
            ScopeSummary: scopeSummary,
            ReviewModel: reviewModel);
    }

    private static ServiceContributionRow BuildServiceRow(ContractDraft draft, TaskKind task)
    {
        var state = DetermineMissingScopeState(draft, task);
        return new ServiceContributionRow(task, state);
    }

    // A service is "Charged" (will be worked) when its scope family has a matching selection;
    // otherwise it reports which scope is missing.
    private static ServiceContributionState DetermineMissingScopeState(ContractDraft draft, TaskKind task)
    {
        if (TaskKindSets.IsAnimalService(task))
            return draft.AnimalBuildings.Count > 0
                ? ServiceContributionState.Charged
                : ServiceContributionState.NeedsAnimalBuildingScope;

        if (TaskKindSets.IsOutdoorService(task) && draft.OutdoorZones.Count > 0)
            return ServiceContributionState.Charged;

        if (TaskKindSets.IsGreenhouseService(task) && draft.Greenhouses.Count > 0)
            return ServiceContributionState.Charged;

        if (TaskKindSets.IsGreenhouseService(task) && !TaskKindSets.IsOutdoorService(task))
            return ServiceContributionState.NeedsGreenhouseScope;

        return ServiceContributionState.NeedsOutdoorScope;
    }

    private static PaymentTimingKind DeterminePaymentTiming(ContractDraft draft)
    {
        if (draft.Schedule == ContractSchedule.Recurring)
        {
            return draft.IsEditing
                ? PaymentTimingKind.RecurringEditAppliesNextEligibleDay
                : PaymentTimingKind.RecurringStartsNextEligibleDay;
        }

        return PaymentTimingKind.OneTimeChargeNow;
    }
}
