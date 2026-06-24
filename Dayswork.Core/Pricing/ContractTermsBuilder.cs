namespace Dayswork.Core.Pricing;

using Dayswork.Core.Config;
using Dayswork.Core.Crops;
using Dayswork.Core.Domain;
using Dayswork.Core.Energy;
using Dayswork.Core.FishPonds;
using Dayswork.Core.Machines;

public sealed class ContractTermsBuilder
{
    private readonly WorkScopeClassifier _scopeClassifier;
    private readonly WorkerEnergyProfileBuilder _energyProfileBuilder;
    private readonly ConfigValueResolver _resolver;

    public ContractTermsBuilder(
        WorkScopeClassifier scopeClassifier,
        WorkerEnergyProfileBuilder energyProfileBuilder,
        ConfigValueResolver resolver)
    {
        _scopeClassifier = scopeClassifier;
        _energyProfileBuilder = energyProfileBuilder;
        _resolver = resolver;
    }

    public ContractTermsSnapshot BuildTerms(
        ContractScopeSelection selection,
        IReadOnlySet<TaskKind> enabledTasks,
        EnergyTier tier,
        ConfigSnapshot config)
    {
        var preview = BuildPreview(selection, enabledTasks, tier, config);
        if (!preview.IsValid || preview.ProposedTerms is null)
            throw new InvalidOperationException("Cannot build contract terms for a selection with no chargeable scope-task pairs.");

        return preview.ProposedTerms;
    }

    public ContractPreview BuildPreview(
        ContractScopeSelection selection,
        IReadOnlySet<TaskKind> enabledTasks,
        EnergyTier tier,
        ConfigSnapshot config,
        CropPlan? cropPlan = null,
        MachineWorkScope? machineScope = null,
        FishPondWorkScope? fishPondScope = null)
    {
        var scopes = _scopeClassifier.Classify(selection, enabledTasks, cropPlan, machineScope, fishPondScope);
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

        var pricingSnapshot = new PricingSnapshot(_resolver.ResolveEnergyTierPrice(config, tier).Value);
        var energyProfile = _energyProfileBuilder.BuildProfile(enabledTasks, tier, config);
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

        // A reload-mode machine group with no input chest can only collect; surface it once
        // (informational — the contract is still chargeable via the collect work).
        if (scopes.Machines is not null
            && scopes.Machines.Groups.Any(group => group.RequiresInput && group.InputChest is null))
        {
            issues.Add(new ContractValidationIssue(ContractValidationCode.MachineGroupNeedsInputChest, null));
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
        var hasManagedCrops = scopes.ManagedCrops is not null;
        var hasMachines = scopes.Machines is not null;
        var hasFishPonds = scopes.FishPonds is not null;
        var hasCave = enabledTasks.Contains(TaskKind.HarvestCave);
        return hasOutdoorPair || hasAnimalPair || hasGreenhousePair || hasManagedCrops || hasMachines || hasFishPonds || hasCave;
    }
}
