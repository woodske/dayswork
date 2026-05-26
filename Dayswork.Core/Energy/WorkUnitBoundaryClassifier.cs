namespace Dayswork.Core.Energy;

public sealed class WorkUnitBoundaryClassifier
{
    public WorkUnitBoundaryDecision EvaluateAfterBeat(
        bool unitResolved,
        WorkerEnergyState energyState,
        bool boundaryStopRequested)
    {
        if (energyState is null) throw new ArgumentNullException(nameof(energyState));

        if (!unitResolved)
        {
            return new WorkUnitBoundaryDecision(
                UnitResolved: false,
                CanContinueCurrentUnit: true,
                ShouldWrapUpAfterCurrentUnit: false,
                CanStartNextUnit: false);
        }

        var canStartNextUnit = !boundaryStopRequested && energyState.CanStartNewWorkUnit;
        return new WorkUnitBoundaryDecision(
            UnitResolved: true,
            CanContinueCurrentUnit: false,
            ShouldWrapUpAfterCurrentUnit: !canStartNextUnit,
            CanStartNextUnit: canStartNextUnit);
    }
}
