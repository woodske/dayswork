namespace Dayswork.Core.Persistence.Dto;

public sealed class MachineInputFilterDtoV1
{
    public bool IsAny { get; set; } = true;
    public List<string> AllowedQualifiedIds { get; set; } = new();
}
