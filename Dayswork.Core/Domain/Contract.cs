namespace Dayswork.Core.Domain;

using Dayswork.Core.Crops;

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
    CropPlan CropPlan
)
{
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
            CropPlan.Empty)
    {
    }
}
