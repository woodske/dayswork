namespace Dayswork.Core.Domain;

using Dayswork.Core.Crops;
using Dayswork.Core.Machines;

public sealed record Contract(
    ContractId Id,
    IReadOnlySet<TaskKind> EnabledTasks,
    IReadOnlyDictionary<TaskKind, DestinationKey> TaskDestinations,
    ContractSchedule Schedule,
    ContractStatus Status,
    GameDate HireDate,
    ContractScopeSelection ScopeSelection,
    ContractTermsSnapshot TermsSnapshot,
    EnergyTier Tier,
    IReadOnlyList<TaskCategory> CategoryPriority,
    CropPlan CropPlan,
    MachineWorkScope MachineScope
)
{
    // Back-compat overload: contracts built without a machine scope (the pre-Manage-Machines call
    // sites and tests) default to an empty scope.
    public Contract(
        ContractId Id,
        IReadOnlySet<TaskKind> EnabledTasks,
        IReadOnlyDictionary<TaskKind, DestinationKey> TaskDestinations,
        ContractSchedule Schedule,
        ContractStatus Status,
        GameDate HireDate,
        ContractScopeSelection ScopeSelection,
        ContractTermsSnapshot TermsSnapshot,
        EnergyTier Tier,
        IReadOnlyList<TaskCategory> CategoryPriority,
        CropPlan CropPlan)
        : this(
            Id,
            EnabledTasks,
            TaskDestinations,
            Schedule,
            Status,
            HireDate,
            ScopeSelection,
            TermsSnapshot,
            Tier,
            CategoryPriority,
            CropPlan,
            MachineWorkScope.Empty)
    {
    }

    // Back-compat overload predating managed crops: no crop plan, no machine scope.
    public Contract(
        ContractId Id,
        IReadOnlySet<TaskKind> EnabledTasks,
        IReadOnlyDictionary<TaskKind, DestinationKey> TaskDestinations,
        ContractSchedule Schedule,
        ContractStatus Status,
        GameDate HireDate,
        ContractScopeSelection ScopeSelection,
        ContractTermsSnapshot TermsSnapshot,
        EnergyTier Tier,
        IReadOnlyList<TaskCategory> CategoryPriority)
        : this(
            Id,
            EnabledTasks,
            TaskDestinations,
            Schedule,
            Status,
            HireDate,
            ScopeSelection,
            TermsSnapshot,
            Tier,
            CategoryPriority,
            CropPlan.Empty,
            MachineWorkScope.Empty)
    {
    }
}
