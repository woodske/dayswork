namespace Dayswork.Core.Persistence.Dto;

public sealed class ContractDtoV2
{
    public string Id { get; set; } = "";
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
