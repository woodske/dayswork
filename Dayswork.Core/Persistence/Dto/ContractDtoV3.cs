namespace Dayswork.Core.Persistence.Dto;

public sealed class ContractDtoV3
{
    public string Id { get; set; } = "";
    // Sponsor (Farmer.UniqueMultiplayerID) and office (Building.id). Absent on schema ≤ 3 saves,
    // which predate ownership — the migration binds those to an office and the host (see
    // ContractAdoption).
    public long? OwnerId { get; set; }
    public string? OfficeId { get; set; }
    public int? Revision { get; set; }
    public List<string> EnabledTasks { get; set; } = new();
    public SortedDictionary<string, DestinationDtoV1> TaskDestinations { get; set; } = new(StringComparer.Ordinal);
    public string Schedule { get; set; } = "";
    public string Status { get; set; } = "";
    public GameDateDtoV1 HireDate { get; set; } = new();
    public ContractScopeSelectionDto ScopeSelection { get; set; } = new();
    public ContractTermsSnapshotDto TermsSnapshot { get; set; } = new();
    public string Tier { get; set; } = "";
    public List<string> CategoryPriority { get; set; } = new();
    public CropPlanDtoV1? CropPlan { get; set; }
    public MachineWorkScopeDtoV1? MachineWorkScope { get; set; }
    public FishPondWorkScopeDtoV1? FishPondWorkScope { get; set; }
    public ContractPreferencesDtoV1? Preferences { get; set; }
}
