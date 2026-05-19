namespace Dayswork.Core.Domain;

public sealed record Contract(
    ContractId Id,
    IReadOnlySet<TaskKind> EnabledTasks,
    IReadOnlyList<Zone> Zones,
    IReadOnlyDictionary<TaskKind, DestinationKey> TaskDestinations,
    ContractSchedule Schedule,
    ContractStatus Status,
    GameDate HireDate,
    int DepositAmount,
    int HourlyRate
);
