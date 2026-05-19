namespace Dayswork.Core.Persistence.Dto;

public sealed class ZoneDtoV1
{
    public string LocationName { get; set; } = "";
    public int TopLeftX { get; set; }
    public int TopLeftY { get; set; }
    public int BottomRightX { get; set; }
    public int BottomRightY { get; set; }
}
