using Dayswork.Core.Domain;
using Dayswork.Core.Inventory;
using Xunit;

namespace Dayswork.Tests.Inventory;

public sealed class OverflowCategorizerTests
{
    [Fact]
    public void Categorize_Deduplicates_And_Orders_By_Reason_Family_Name()
    {
        var overflow = new[]
        {
            new OverflowItem(
                new RoutedItemStack("(O)388", 1, TaskKind.CutTrees, OutputScopeProvenance.Outdoor()),
                OverflowReason.ChestMissing),
            new OverflowItem(
                new RoutedItemStack("(O)390", 2, TaskKind.ClearRocks, OutputScopeProvenance.Outdoor()),
                OverflowReason.ChestMissing),
            new OverflowItem(
                new RoutedItemStack("(O)430", 1, TaskKind.CollectAnimalProducts, OutputScopeProvenance.AnimalBuilding("Barn")),
                OverflowReason.ChestFull),
            new OverflowItem(
                new RoutedItemStack("(O)709", 1, TaskKind.HarvestCrops, OutputScopeProvenance.Greenhouse("Greenhouse")),
                OverflowReason.NotDelivered),
        };

        var categories = new OverflowCategorizer().Categorize(overflow);

        Assert.Equal(
            new[]
            {
                new OverflowCategory(OverflowReason.ChestFull, OutputScopeFamily.AnimalBuilding, "Barn"),
                new OverflowCategory(OverflowReason.ChestMissing, OutputScopeFamily.Outdoor, "Farm"),
                new OverflowCategory(OverflowReason.NotDelivered, OutputScopeFamily.Greenhouse, "Greenhouse"),
            },
            categories);
    }
}
