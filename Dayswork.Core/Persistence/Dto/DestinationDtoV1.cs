namespace Dayswork.Core.Persistence.Dto;

public sealed class DestinationDtoV1
{
    public string Type { get; set; } = "";
    public string? LocationName { get; set; }
    public int? X { get; set; }
    public int? Y { get; set; }
}
