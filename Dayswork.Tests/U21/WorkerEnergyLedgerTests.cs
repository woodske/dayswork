namespace Dayswork.Tests.U21;

using Dayswork.Core.Energy;
using Xunit;

public sealed class WorkerEnergyLedgerTests
{
    [Fact]
    public void StartShift_initializes_full_capacity_state()
    {
        var ledger = new WorkerEnergyLedger();
        var profile = U21PropertyGenerators.BuildProfile(12);

        var state = ledger.StartShift(profile);

        Assert.Equal(12, state.Capacity);
        Assert.Equal(12, state.RemainingEnergy);
        Assert.True(state.CanStartNewWorkUnit);
        Assert.Equal(profile.ActionCosts.Count, state.ActionCosts.Count);
    }

    [Fact]
    public void ApplyActionCost_clamps_at_zero_and_preserves_exhaustion()
    {
        var ledger = new WorkerEnergyLedger();
        var profile = U21PropertyGenerators.BuildProfile(1);

        var initial = ledger.StartShift(profile);
        var spentToZero = ledger.ApplyActionCost(initial, WorkActionKind.AxeSwing);
        var spentAgain = ledger.ApplyActionCost(spentToZero.State, WorkActionKind.HarvestCrop);

        Assert.Equal(0, spentToZero.State.RemainingEnergy);
        Assert.False(spentToZero.State.CanStartNewWorkUnit);
        Assert.True(spentToZero.ReachedZeroOnThisBeat);
        Assert.Equal(0, spentAgain.State.RemainingEnergy);
        Assert.False(spentAgain.State.CanStartNewWorkUnit);
    }
}
