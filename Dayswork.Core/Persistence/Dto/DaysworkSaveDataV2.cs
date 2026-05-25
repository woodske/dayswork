namespace Dayswork.Core.Persistence.Dto;

public sealed class DaysworkSaveDataV2
{
    public int SchemaVersion { get; set; } = 2;
    public string ModVersion { get; set; } = "";
    public List<ContractDtoV2> Contracts { get; set; } = new();
}
