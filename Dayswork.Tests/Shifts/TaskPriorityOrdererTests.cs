using Dayswork.Core.Domain;
using Dayswork.Core.Shifts;
using FsCheck;
using FsCheck.Xunit;
using Xunit;

namespace Dayswork.Tests.Shifts;

public class TaskPriorityOrdererTests
{
    private readonly ITaskPriorityOrderer _sut = new TaskPriorityOrderer();

    // Default category priority [AnimalCare, Crops, Fieldwork], with tasks ordered by enum value
    // within each category (the within-category tie-break in TaskPriorityOrderer.Order).
    private static readonly TaskKind[] Fr_Work_03_Order = new[]
    {
        TaskKind.FeedAnimals,
        TaskKind.PetAnimals,
        TaskKind.CollectAnimalProducts,
        TaskKind.WaterCrops,
        TaskKind.HarvestCrops,
        TaskKind.CollectFruit,
        TaskKind.CutTrees,
        TaskKind.ClearRocks,
        TaskKind.ClearWeeds,
        TaskKind.ClearGrass,
    };

    // ── Fact tests ───────────────────────────────────────────────────────────

    [Fact]
    public void AllTenTasks_ReturnInFRWork03Order()
    {
        var all = new[]
        {
            TaskKind.CutTrees,           // deliberately scrambled input
            TaskKind.WaterCrops,
            TaskKind.FeedAnimals,
            TaskKind.ClearRocks,
            TaskKind.HarvestCrops,
            TaskKind.PetAnimals,
            TaskKind.ClearWeeds,
            TaskKind.CollectFruit,
            TaskKind.ClearGrass,
            TaskKind.CollectAnimalProducts,
        };

        var result = _sut.Order(all);

        Assert.Equal(Fr_Work_03_Order, result);
    }

    [Fact]
    public void SingleTask_ReturnsOneElementList()
    {
        var result = _sut.Order(new[] { TaskKind.ClearGrass });

        Assert.Single(result);
        Assert.Equal(TaskKind.ClearGrass, result[0]);
    }

    [Fact]
    public void EmptyInput_ReturnsEmptyList()
    {
        var result = _sut.Order(System.Array.Empty<TaskKind>());

        Assert.Empty(result);
    }

    // ── PBT-03 properties ────────────────────────────────────────────────────

    // Arbitrary that generates a non-empty subset of TaskKind values
    private static Arbitrary<TaskKind[]> ArbTaskSubset =>
        Arb.From(
            from count in Gen.Choose(1, 10)
            from subset in Gen.Shuffle(Fr_Work_03_Order).Select(arr => arr.Take(count).ToArray())
            select subset);

    [Property(MaxTest = 1000)]
    public Property AnySubset_OrderIsDeterministic()
    {
        return Prop.ForAll(ArbTaskSubset, subset =>
        {
            var first  = _sut.Order(subset);
            var second = _sut.Order(subset);
            return first.SequenceEqual(second);
        });
    }

    [Property(MaxTest = 1000)]
    public Property AnySubset_OutputIsInSpecPriorityOrder()
    {
        // Build rank lookup for assertion
        var rank = Fr_Work_03_Order
            .Select((kind, i) => (kind, i))
            .ToDictionary(x => x.kind, x => x.i);

        return Prop.ForAll(ArbTaskSubset, subset =>
        {
            var result = _sut.Order(subset);
            for (int i = 0; i < result.Count - 1; i++)
            {
                if (rank[result[i]] >= rank[result[i + 1]])
                    return false;
            }
            return true;
        });
    }
}
