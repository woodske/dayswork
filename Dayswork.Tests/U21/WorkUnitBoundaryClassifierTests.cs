namespace Dayswork.Tests.U21;

using Dayswork.Core.Energy;
using Xunit;

public sealed class WorkUnitBoundaryClassifierTests
{
    [Fact]
    public void Unresolved_unit_continues_even_when_boundary_stop_is_requested()
    {
        var classifier = new WorkUnitBoundaryClassifier();
        var state = WorkerEnergyState.FromProfile(U21PropertyGenerators.BuildProfile(0));

        var decision = classifier.EvaluateAfterBeat(
            unitResolved: false,
            energyState: state with { CanStartNewWorkUnit = false },
            boundaryStopRequested: true);

        Assert.True(decision.CanContinueCurrentUnit);
        Assert.False(decision.ShouldWrapUpAfterCurrentUnit);
        Assert.False(decision.CanStartNextUnit);
    }

    [Fact]
    public void Resolved_unit_wraps_up_when_stop_is_requested()
    {
        var classifier = new WorkUnitBoundaryClassifier();
        var state = WorkerEnergyState.FromProfile(U21PropertyGenerators.BuildProfile(10));

        var decision = classifier.EvaluateAfterBeat(
            unitResolved: true,
            energyState: state,
            boundaryStopRequested: true);

        Assert.False(decision.CanContinueCurrentUnit);
        Assert.True(decision.ShouldWrapUpAfterCurrentUnit);
        Assert.False(decision.CanStartNextUnit);
    }

    [Fact]
    public void Resolved_unit_can_start_next_when_energy_remains_and_no_stop_is_requested()
    {
        var classifier = new WorkUnitBoundaryClassifier();
        var state = WorkerEnergyState.FromProfile(U21PropertyGenerators.BuildProfile(10));

        var decision = classifier.EvaluateAfterBeat(
            unitResolved: true,
            energyState: state,
            boundaryStopRequested: false);

        Assert.False(decision.CanContinueCurrentUnit);
        Assert.False(decision.ShouldWrapUpAfterCurrentUnit);
        Assert.True(decision.CanStartNextUnit);
    }
}
