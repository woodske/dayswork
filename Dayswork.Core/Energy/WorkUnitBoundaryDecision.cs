namespace Dayswork.Core.Energy;

public sealed record WorkUnitBoundaryDecision(
    bool UnitResolved,
    bool CanContinueCurrentUnit,
    bool ShouldWrapUpAfterCurrentUnit,
    bool CanStartNextUnit);
