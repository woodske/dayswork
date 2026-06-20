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
    MachineWorkScope MachineScope,
    ContractPreferences Preferences
)
{
    // Back-compat overload: contracts built without preferences default to Legacy (preserve old behavior).
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
        CropPlan CropPlan,
        MachineWorkScope MachineScope)
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
            MachineScope,
            ContractPreferences.Legacy)
    {
    }

    // Back-compat overload: contracts built without a machine scope default to an empty scope.
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
            MachineWorkScope.Empty,
            ContractPreferences.Legacy)
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
            MachineWorkScope.Empty,
            ContractPreferences.Legacy)
    {
    }
}
