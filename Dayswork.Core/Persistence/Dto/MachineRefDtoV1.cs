namespace Dayswork.Core.Persistence.Dto;

public sealed class MachineRefDtoV1
{
    public string LocationName { get; set; } = "";
    public int X { get; set; }
    public int Y { get; set; }
    public string ExpectedQualifiedId { get; set; } = "";
}
