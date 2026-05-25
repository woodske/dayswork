using Dayswork.Core.Domain;

namespace Dayswork.UI;

internal static class HiringFlowViewModelBuilder
{
    public static DraftPreviewState Build(ContractDraft draft, ContractPreview preview)
    {
        var lineItemsByService = (preview.ProposedTerms?.Pricing.LineItems ?? Array.Empty<PricingLineItem>())
            .GroupBy(line => line.Service)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<PricingLineItem>)group
                    .OrderBy(line => line.Family)
                    .ThenBy(line => line.OutdoorBand)
                    .ThenBy(line => line.AnimalTier)
                    .ThenBy(line => line.LineTotal)
                    .ToList()
                    .AsReadOnly());

        var serviceRows = TaskPresentation.TaskOrder
            .Where(draft.EnabledTasks.Contains)
            .Select(task => BuildServiceRow(draft, task, lineItemsByService))
            .ToList()
            .AsReadOnly();

        var scopeSummary = new ScopeSummaryModel(
            draft.ScopeSelection.OutdoorZones,
            draft.ScopeSelection.AnimalBuildings,
            draft.ScopeSelection.Greenhouse);

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
            PaymentTimingKind: DeterminePaymentTiming(draft),
            ValidationMessages: validationMessages,
            CanConfirm: preview.IsValid);

        return new DraftPreviewState(
            Preview: preview,
            ServiceRows: serviceRows,
            ScopeSummary: scopeSummary,
            ReviewModel: reviewModel);
    }

    private static ServiceContributionRow BuildServiceRow(
        ContractDraft draft,
        TaskKind task,
        IReadOnlyDictionary<TaskKind, IReadOnlyList<PricingLineItem>> lineItemsByService)
    {
        if (lineItemsByService.TryGetValue(task, out var pricingLines))
        {
            return new ServiceContributionRow(
                Service: task,
                RowState: ServiceContributionState.Charged,
                PricingLines: pricingLines,
                DisplayAmount: pricingLines.Sum(line => line.LineTotal));
        }

        return new ServiceContributionRow(
            Service: task,
            RowState: DetermineMissingScopeState(draft, task),
            PricingLines: Array.Empty<PricingLineItem>(),
            DisplayAmount: null);
    }

    private static ServiceContributionState DetermineMissingScopeState(ContractDraft draft, TaskKind task)
    {
        if (TaskKindSets.IsAnimalService(task))
            return ServiceContributionState.NeedsAnimalBuildingScope;

        if (TaskKindSets.IsOutdoorService(task) && draft.OutdoorZones.Count == 0)
            return ServiceContributionState.NeedsOutdoorScope;

        if (TaskKindSets.IsGreenhouseService(task) && draft.Greenhouse is null)
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
