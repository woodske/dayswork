namespace Dayswork.Core.Domain;

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
    IReadOnlyList<TaskCategory> CategoryPriority
);
