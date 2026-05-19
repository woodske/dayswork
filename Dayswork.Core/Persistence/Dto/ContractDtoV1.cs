namespace Dayswork.Core.Persistence.Dto;

public sealed class ContractDtoV1
{
    public string Id { get; set; } = "";
    public List<string> EnabledTasks { get; set; } = new();
    public List<ZoneDtoV1> Zones { get; set; } = new();
    public Dictionary<string, DestinationDtoV1> TaskDestinations { get; set; } = new();
    public string Schedule { get; set; } = "";
    public string Status { get; set; } = "";
    public GameDateDtoV1 HireDate { get; set; } = new();
    public int DepositAmount { get; set; }
    public int HourlyRate { get; set; }
}
