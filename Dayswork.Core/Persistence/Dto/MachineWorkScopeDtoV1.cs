namespace Dayswork.Core.Persistence.Dto;

public sealed class MachineWorkScopeDtoV1
{
    public List<MachineGroupDtoV1> Groups { get; set; } = new();
}
