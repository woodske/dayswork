namespace Dayswork.Core.Persistence.Dto;

public sealed class ContractPreferencesDtoV1
{
    public bool? AvoidBlueGrass { get; set; }

    // Enum serialized as its name; null on saves from before the idle-task preference existed.
    public string? IdleTask { get; set; }

    // Player-chosen worker name; null/absent on saves from before multi-farmhand naming existed
    // (and omitted on write while unset) — maps to "" in the domain record.
    public string? WorkerName { get; set; }
}
