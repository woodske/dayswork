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
    public LegacyFinancialBridgeDto LegacyFinancialBridge { get; set; } = new();
}
