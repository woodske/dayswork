namespace Dayswork.Core.Persistence.Dto;

public sealed class ContractPreferencesDtoV1
{
    public bool? AvoidBlueGrass { get; set; }

    // Enum serialized as its name; null on saves from before the idle-task preference existed.
    public string? IdleTask { get; set; }

    // Player-chosen worker name; null/absent on saves from before worker naming existed
    // (and omitted on write while unset) — maps to "" in the domain record.
    public string? WorkerName { get; set; }

    // Owner-scoped 2.0 preferences; null/absent on schema ≤ 3 saves, where they take their
    // domain defaults (run while offline, grant XP, no appearance variant chosen).
    public bool? RunWhileOwnerOffline { get; set; }
    public bool? GrantExperience { get; set; }
    public string? Appearance { get; set; }
}
