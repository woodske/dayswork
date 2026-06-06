using Dayswork.Core.Crops;
using Dayswork.Core.Domain;
using Dayswork.Core.Energy;
using Xunit;

namespace Dayswork.Tests.ManageCrops;

public sealed class ManagedCropActionMapTests
{
    [Theory]
    [InlineData(ManagedCropActionKind.Harvest, WorkActionKind.HarvestCrop, WorkerTool.None, false)]
    [InlineData(ManagedCropActionKind.Till, WorkActionKind.HoeSwing, WorkerTool.Hoe, true)]
    [InlineData(ManagedCropActionKind.Fertilize, WorkActionKind.ApplyFertilizer, WorkerTool.None, false)]
    [InlineData(ManagedCropActionKind.PlantSeed, WorkActionKind.PlantSeed, WorkerTool.None, false)]
    [InlineData(ManagedCropActionKind.Water, WorkActionKind.WaterTile, WorkerTool.WateringCan, true)]
    public void MapsActionToExpectedEnergyToolAndGate(
        ManagedCropActionKind kind, WorkActionKind expectedEnergy, WorkerTool expectedTool, bool expectedGated)
    {
        Assert.Equal(expectedEnergy, ManagedCropActionMap.EnergyKind(kind));
        Assert.Equal(expectedTool, ManagedCropActionMap.Tool(kind));
        Assert.Equal(expectedGated, ManagedCropActionMap.IsToolGated(kind));
    }

    [Theory]
    [InlineData(WorkerTool.Axe, WorkActionKind.AxeSwing)]
    [InlineData(WorkerTool.Pickaxe, WorkActionKind.PickaxeSwing)]
    [InlineData(WorkerTool.Scythe, WorkActionKind.ScytheSwing)]
    [InlineData(WorkerTool.None, WorkActionKind.ScytheSwing)]
    public void ClearDebris_EnergyDependsOnLiveDebrisTool(WorkerTool debrisTool, WorkActionKind expected)
    {
        Assert.Equal(expected, ManagedCropActionMap.EnergyKind(ManagedCropActionKind.ClearDebris, debrisTool));
        Assert.Equal(debrisTool, ManagedCropActionMap.Tool(ManagedCropActionKind.ClearDebris, debrisTool));
        Assert.True(ManagedCropActionMap.IsToolGated(ManagedCropActionKind.ClearDebris));
    }

    [Fact]
    public void Mapping_IsTotalAndDeterministic_OverEveryActionAndDebrisTool()
    {
        foreach (var kind in Enum.GetValues<ManagedCropActionKind>())
        foreach (var tool in Enum.GetValues<WorkerTool>())
        {
            // Total: never throws for any combination.
            var energy1 = ManagedCropActionMap.EnergyKind(kind, tool);
            var energy2 = ManagedCropActionMap.EnergyKind(kind, tool);
            var mappedTool1 = ManagedCropActionMap.Tool(kind, tool);
            var mappedTool2 = ManagedCropActionMap.Tool(kind, tool);

            // Deterministic: same input → same output.
            Assert.Equal(energy1, energy2);
            Assert.Equal(mappedTool1, mappedTool2);
        }
    }

    [Fact]
    public void PlantingAndFertilizingAndHarvest_AreNotToolGated()
    {
        Assert.False(ManagedCropActionMap.IsToolGated(ManagedCropActionKind.PlantSeed));
        Assert.False(ManagedCropActionMap.IsToolGated(ManagedCropActionKind.Fertilize));
        Assert.False(ManagedCropActionMap.IsToolGated(ManagedCropActionKind.Harvest));
    }
}
