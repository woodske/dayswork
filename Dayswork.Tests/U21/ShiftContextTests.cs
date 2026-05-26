namespace Dayswork.Tests.U21;

using Dayswork.Core.Domain;
using Dayswork.Core.Energy;
using Dayswork.Core.Shifts;
using Xunit;

public sealed class ShiftContextTests
{
    [Fact]
    public void Context_anchors_terms_energy_and_pacing()
    {
        var terms = U21PropertyGenerators.BuildTermsSnapshot(18);
        var energy = WorkerEnergyState.FromProfile(terms.Energy);
        var pacing = new WorkerPacingProfile(2f, 650d, 120);

        var context = new ShiftContext(
            ContractId.New(),
            new WorkScopeSet(null, Array.Empty<AnimalBuildingScope>(), null),
            new HashSet<TaskKind>(),
            new Dictionary<TaskKind, DestinationKey>(),
            contractTerms: terms,
            energyState: energy,
            pacingProfile: pacing,
            toolSnapshot: new ToolSnapshot(ToolLevel.Basic, ToolLevel.Basic, ToolLevel.Basic),
            workList: Array.Empty<WorkItem>(),
            shiftStartTime: 600);

        Assert.Equal(terms, context.ContractTerms);
        Assert.Equal(18, context.EnergyState.RemainingEnergy);
        Assert.Equal(2f, context.PacingProfile.WalkPixelsPerTick);
    }
}
