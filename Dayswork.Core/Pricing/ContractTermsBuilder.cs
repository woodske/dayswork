namespace Dayswork.Core.Pricing;

using Dayswork.Core.Config;
using Dayswork.Core.Domain;
using Dayswork.Core.Energy;

public sealed class ContractTermsBuilder : IContractTermsBuilder
{
    private readonly IWorkScopeClassifier _scopeClassifier;
    private readonly IOutdoorServiceBandClassifier _outdoorBandClassifier;
    private readonly IContractPriceCalculator _priceCalculator;
    private readonly IPriceBreakdownBuilder _priceBreakdownBuilder;
    private readonly IWorkerEnergyProfileBuilder _energyProfileBuilder;

    public ContractTermsBuilder(
        IWorkScopeClassifier scopeClassifier,
        IOutdoorServiceBandClassifier outdoorBandClassifier,
        IContractPriceCalculator priceCalculator,
        IPriceBreakdownBuilder priceBreakdownBuilder,
        IWorkerEnergyProfileBuilder energyProfileBuilder)
    {
        _scopeClassifier = scopeClassifier;
        _outdoorBandClassifier = outdoorBandClassifier;
        _priceCalculator = priceCalculator;
        _priceBreakdownBuilder = priceBreakdownBuilder;
        _energyProfileBuilder = energyProfileBuilder;
    }

    public ContractTermsSnapshot BuildTerms(
        ContractScopeSelection selection,
        IReadOnlySet<TaskKind> enabledTasks,
        IConfigSnapshot config)
    {
        var preview = BuildPreview(selection, enabledTasks, config);
        if (!preview.IsValid || preview.ProposedTerms is null)
            throw new InvalidOperationException("Cannot build contract terms for a selection with no chargeable scope-task pairs.");

        return preview.ProposedTerms;
    }

    public ContractPreview BuildPreview(
        ContractScopeSelection selection,
        IReadOnlySet<TaskKind> enabledTasks,
        IConfigSnapshot config)
    {
        var scopes = _scopeClassifier.Classify(selection, enabledTasks);
        var issues = BuildValidationIssues(scopes, enabledTasks);
        if (!HasChargeableScopeTaskPair(scopes, enabledTasks))
        {
            var blockingIssues = new List<ContractValidationIssue>(issues)
            {
                new(ContractValidationCode.NoChargeableScopeTaskPair, null),
            };

            return new ContractPreview(
                IsValid: false,
                ValidationIssues: blockingIssues,
                ProposedTerms: null);
        }

        var outdoorBands = _outdoorBandClassifier.ClassifyBands(scopes, enabledTasks, config);
        var totals = _priceCalculator.Calculate(scopes, enabledTasks, outdoorBands, config);
        var pricingSnapshot = _priceBreakdownBuilder.BuildSnapshot(scopes, enabledTasks, outdoorBands, totals, config);
        var energyProfile = _energyProfileBuilder.BuildProfile(enabledTasks, config);
        var proposedTerms = new ContractTermsSnapshot(pricingSnapshot, energyProfile);

        return new ContractPreview(
            IsValid: true,
            ValidationIssues: issues,
            ProposedTerms: proposedTerms);
    }

    private static IReadOnlyList<ContractValidationIssue> BuildValidationIssues(
        WorkScopeSet scopes,
        IReadOnlySet<TaskKind> enabledTasks)
    {
        var issues = new List<ContractValidationIssue>();

        if (scopes.OutdoorWork is null)
        {
            issues.AddRange(
                TaskKindSets.OutdoorServices
                    .Where(enabledTasks.Contains)
                    .Select(task => new ContractValidationIssue(
                        ContractValidationCode.NoOutdoorScopeForSelectedOutdoorService,
                        task)));
        }

        if (scopes.AnimalBuildings.Count == 0)
        {
            issues.AddRange(
                TaskKindSets.AnimalServices
                    .Where(enabledTasks.Contains)
                    .Select(task => new ContractValidationIssue(
                        ContractValidationCode.NoAnimalBuildingForSelectedAnimalService,
                        task)));
        }

        if (scopes.GreenhouseWork is null && enabledTasks.Any(TaskKindSets.IsGreenhouseService))
        {
            issues.AddRange(
                TaskKindSets.GreenhouseServices
                    .Where(enabledTasks.Contains)
                    .Where(task => !TaskKindSets.IsOutdoorService(task))
                    .Select(task => new ContractValidationIssue(
                        ContractValidationCode.NoGreenhouseScopeForSelectedGreenhouseService,
                        task)));
        }

        return issues;
    }

    private static bool HasChargeableScopeTaskPair(
        WorkScopeSet scopes,
        IReadOnlySet<TaskKind> enabledTasks)
    {
        var hasOutdoorPair = scopes.OutdoorWork is not null && enabledTasks.Any(TaskKindSets.IsOutdoorService);
        var hasAnimalPair = scopes.AnimalBuildings.Count > 0 && enabledTasks.Any(TaskKindSets.IsAnimalService);
        var hasGreenhousePair = scopes.GreenhouseWork is not null && enabledTasks.Any(TaskKindSets.IsGreenhouseService);
        return hasOutdoorPair || hasAnimalPair || hasGreenhousePair;
    }
}
