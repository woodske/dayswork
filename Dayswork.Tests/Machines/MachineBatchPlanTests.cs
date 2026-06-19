namespace Dayswork.Tests.Machines;

using Dayswork.Core.Domain;
using Dayswork.Core.Machines;
using Dayswork.Core.Shifts;
using Xunit;

public sealed class MachineBatchPlanTests
{
    private readonly ShiftPlanBuilder _sut = new();

    private static WorkScopeSet ScopesWithMachines(MachineWorkScope machines) =>
        WorkScopeSet.WithSingleGreenhouse(
            outdoorWork: null,
            animalBuildings: Array.Empty<AnimalBuildingScope>(),
            greenhouseWork: null,
            managedCrops: null,
            machines: machines);

    private static MachineGroup Group(string id, params (string Location, int X, int Y)[] machines) =>
        new(id, machines.Select(m => new MachineRef(m.Location, new TileCoord(m.X, m.Y), "(BC)16")).ToList());

    [Fact]
    public void MachinesSpanningTwoLocations_EmitsOneBatchPerLocation()
    {
        var scope = new MachineWorkScope(new[]
        {
            Group("group-a", ("Farm", 10, 10), ("Shed", 2, 2)),
            Group("group-b", ("Farm", 12, 12)),
        });

        var result = _sut.BuildBatchPlan(ScopesWithMachines(scope), new HashSet<TaskKind>(), TaskKindSets.DefaultCategoryPriority);

        Assert.Equal(2, result.Count);
        Assert.All(result, batch => Assert.Equal(BatchKind.Machines, batch.Kind));
        Assert.All(result, batch => Assert.Empty(batch.Tasks));
        Assert.Equal(new[] { "Farm", "Shed" }, result.Select(batch => batch.LocationName));
    }

    [Fact]
    public void NoMachineScope_EmitsNoMachineBatch()
    {
        var result = _sut.BuildBatchPlan(
            ScopesWithMachines(MachineWorkScope.Empty),
            new HashSet<TaskKind>(),
            TaskKindSets.DefaultCategoryPriority);

        Assert.Empty(result);
    }

    [Fact]
    public void CategoryPriority_OrdersMachineBatchesRelativeToOthers()
    {
        var scope = new MachineWorkScope(new[] { Group("group-a", ("Farm", 5, 5)) });
        var scopes = new WorkScopeSet(
            OutdoorWork: new OutdoorWorkScope(new[] { new Zone("Farm", new TileCoord(0, 0), new TileCoord(1, 1)) }, 1),
            AnimalBuildings: Array.Empty<AnimalBuildingScope>(),
            GreenhouseWorks: Array.Empty<GreenhouseWorkScope>(),
            ManagedCrops: null,
            Machines: scope);

        var machinesFirst = new[] { TaskCategory.Machines, TaskCategory.Crops, TaskCategory.AnimalCare, TaskCategory.Fieldwork };
        var result = _sut.BuildBatchPlan(scopes, new HashSet<TaskKind> { TaskKind.HarvestCrops }, machinesFirst);

        Assert.Equal(BatchKind.Machines, result[0].Kind);
        Assert.Contains(result, batch => batch.Kind == BatchKind.OutdoorCrops);
    }
}
