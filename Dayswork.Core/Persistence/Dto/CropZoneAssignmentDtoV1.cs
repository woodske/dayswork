namespace Dayswork.Core.Persistence.Dto;

public sealed class CropZoneAssignmentDtoV1
{
    public string LocationName { get; set; } = "";
    public int TopLeftX { get; set; }
    public int TopLeftY { get; set; }
    public int BottomRightX { get; set; }
    public int BottomRightY { get; set; }
    public string Mode { get; set; } = "";
    public List<SeasonCropChoiceDtoV1> Choices { get; set; } = new();
    public ChestRefDtoV1? OutputChest { get; set; }
    public string? GroupId { get; set; }
}
