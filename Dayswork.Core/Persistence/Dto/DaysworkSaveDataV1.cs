namespace Dayswork.Core.Persistence.Dto;

public sealed class DaysworkSaveDataV1
{
    public int SchemaVersion { get; set; } = 1;
    public string ModVersion { get; set; } = "";
    public List<ContractDtoV1> Contracts { get; set; } = new();
}
