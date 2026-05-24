namespace Dayswork.Tests.Pricing;

using Dayswork.Core.Config;
using Dayswork.Core.Domain;
using Dayswork.Core.Energy;
using Dayswork.Tests.Generators;
using FsCheck;
using FsCheck.Xunit;

public sealed class ContractTermsPropertyTests
{
    [Property(MaxTest = 300)]
    public Property BuildPreview_IsDeterministic() =>
        Prop.ForAll(
            U18ContractTermsGen.ScopeSelection(),
            U18ContractTermsGen.EnabledTaskSet(),
            ConfigSnapshotGen.Snapshot(),
            (selection, enabledTasks, config) =>
            {
                var builder = U18BuilderFactory.CreateTermsBuilder();
                var first = builder.BuildPreview(selection, enabledTasks, config);
                var second = builder.BuildPreview(selection, enabledTasks, config);
                return DescribePreview(first) == DescribePreview(second);
            });

    [Property(MaxTest = 300)]
    public Property PricingSnapshot_TotalsReconcile() =>
        Prop.ForAll(
            U18ContractTermsGen.ScopeSelection(),
            U18ContractTermsGen.EnabledTaskSet(),
            ConfigSnapshotGen.Snapshot(),
            (selection, enabledTasks, config) =>
            {
                var preview = U18BuilderFactory.CreateTermsBuilder().BuildPreview(selection, enabledTasks, config);
                if (!preview.IsValid || preview.ProposedTerms is null)
                    return true;

                var pricing = preview.ProposedTerms.Pricing;
                var outdoorSum = pricing.LineItems
                    .Where(line => line.Family == PricingFamily.Outdoor)
                    .Sum(line => line.LineTotal);
                var animalSum = pricing.LineItems
                    .Where(line => line.Family == PricingFamily.AnimalBuilding)
                    .Sum(line => line.LineTotal);
                var greenhouseSum = pricing.LineItems
                    .Where(line => line.Family == PricingFamily.Greenhouse)
                    .Sum(line => line.LineTotal);

                return pricing.OutdoorSubtotal == outdoorSum
                    && pricing.AnimalSubtotal == animalSum
                    && pricing.GreenhouseSubtotal == greenhouseSum
                    && pricing.TotalPrice == pricing.LineItems.Sum(line => line.LineTotal);
            });

    [Property(MaxTest = 300)]
    public Property EquivalentOutdoorUnions_HaveEquivalentPricing() =>
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

                var builder = U18BuilderFactory.CreateTermsBuilder();
                var left = builder.BuildPreview(equivalentSelections.Left, enabledTasks, config);
                var right = builder.BuildPreview(equivalentSelections.Right, enabledTasks, config);
                return DescribePreview(left) == DescribePreview(right);
            });

    [Property(MaxTest = 300)]
    public Property PreviewValidity_IffAnyChargeablePairExists() =>
        Prop.ForAll(
            U18ContractTermsGen.ScopeSelection(),
            U18ContractTermsGen.EnabledTaskSet(),
            ConfigSnapshotGen.Snapshot(),
            (selection, enabledTasks, config) =>
            {
                var expected =
                    (selection.OutdoorZones.Count > 0 && enabledTasks.Any(TaskKindSets.IsOutdoorService))
                    || (selection.AnimalBuildings.Count > 0 && enabledTasks.Any(TaskKindSets.IsAnimalService))
                    || (selection.Greenhouse is not null && enabledTasks.Any(TaskKindSets.IsGreenhouseService));

                var actual = U18BuilderFactory.CreateTermsBuilder()
                    .BuildPreview(selection, enabledTasks, config)
                    .IsValid;

                return actual == expected;
            });

    [Property(MaxTest = 300)]
    public Property ValidTermsSnapshot_PreservesFullActionCostTable() =>
        Prop.ForAll(
            ConfigSnapshotGen.Snapshot(),
            config =>
            {
                var preview = U18BuilderFactory.CreateTermsBuilder().BuildPreview(
                    new ContractScopeSelection(
                        OutdoorZones: new[] { new Zone("Farm", new TileCoord(0, 0), new TileCoord(0, 0)) },
                        AnimalBuildings: Array.Empty<AnimalBuildingSelection>(),
                        Greenhouse: null),
                    new HashSet<TaskKind> { TaskKind.WaterCrops },
                    config);

                if (!preview.IsValid || preview.ProposedTerms is null)
                    return false;

                var actionCosts = preview.ProposedTerms.Energy.ActionCosts;
                return actionCosts.Count == Enum.GetValues<WorkActionKind>().Length
                    && Enum.GetValues<WorkActionKind>().All(action => actionCosts.ContainsKey(action))
                    && Enum.GetValues<WorkActionKind>().All(action => actionCosts[action] == config.WorkActionCosts[action]);
            });

    private static string DescribePreview(ContractPreview preview)
    {
        if (!preview.IsValid || preview.ProposedTerms is null)
        {
            var invalidIssues = string.Join(
                ",",
                preview.ValidationIssues
                    .OrderBy(issue => issue.Code)
                    .ThenBy(issue => issue.RelatedTask)
                    .Select(issue => $"{issue.Code}:{issue.RelatedTask?.ToString() ?? "none"}"));
            return $"invalid|{invalidIssues}";
        }

        var pricing = preview.ProposedTerms.Pricing;
        var lines = string.Join(
            ";",
            pricing.LineItems.Select(line =>
                $"{line.Family}:{line.Service}:{line.Quantity}:{line.UnitPrice}:{line.LineTotal}:{line.OutdoorBand?.ToString() ?? "none"}:{line.AnimalTier?.ToString() ?? "none"}"));
        var actionCosts = string.Join(
            ";",
            preview.ProposedTerms.Energy.ActionCosts
                .OrderBy(kvp => kvp.Key)
                .Select(kvp => $"{kvp.Key}:{kvp.Value}"));

        return $"valid|{pricing.OutdoorSubtotal}|{pricing.AnimalSubtotal}|{pricing.GreenhouseSubtotal}|{pricing.TotalPrice}|{lines}|{preview.ProposedTerms.Energy.DailyCapacity}|{actionCosts}";
    }
}
