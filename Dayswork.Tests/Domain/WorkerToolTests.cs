using Dayswork.Core.Domain;
using Xunit;

namespace Dayswork.Tests.Domain;

public class WorkerToolTests
{
    public static IEnumerable<object[]> ExpectedTools => new[]
    {
        new object[] { TaskKind.WaterCrops, WorkerTool.WateringCan },
        new object[] { TaskKind.HarvestCrops, WorkerTool.None },
        new object[] { TaskKind.CollectFruit, WorkerTool.None },
        new object[] { TaskKind.FeedAnimals, WorkerTool.None },
        new object[] { TaskKind.PetAnimals, WorkerTool.None },
        new object[] { TaskKind.CollectAnimalProducts, WorkerTool.None },
        new object[] { TaskKind.CutTrees, WorkerTool.Axe },
        new object[] { TaskKind.ClearRocks, WorkerTool.Pickaxe },
        new object[] { TaskKind.ClearWeeds, WorkerTool.Scythe },
        new object[] { TaskKind.ClearGrass, WorkerTool.Scythe },
    };

    [Theory]
    [MemberData(nameof(ExpectedTools))]
    public void ForTask_ReturnsExpectedTool(TaskKind task, WorkerTool expected)
    {
        Assert.Equal(expected, WorkerToolExtensions.ForTask(task));
    }

    [Fact]
    public void ExpectedTools_CoversEveryTaskKind()
    {
        var covered = ExpectedTools.Select(row => (TaskKind)row[0]).OrderBy(t => t).ToArray();
        var all = Enum.GetValues<TaskKind>().OrderBy(t => t).ToArray();

        Assert.Equal(all, covered);
    }
}
