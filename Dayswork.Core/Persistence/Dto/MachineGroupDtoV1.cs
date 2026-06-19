namespace Dayswork.Core.Persistence.Dto;

public sealed class MachineGroupDtoV1
{
    public string Id { get; set; } = "";
    public string MachineType { get; set; } = "";
    public List<MachineRefDtoV1> Machines { get; set; } = new();
    public MachineInputFilterDtoV1 InputFilter { get; set; } = new();
    public ChestRefDtoV1? InputChest { get; set; }
    public DestinationDtoV1 OutputDestination { get; set; } = new();
    public string Mode { get; set; } = "";
}
