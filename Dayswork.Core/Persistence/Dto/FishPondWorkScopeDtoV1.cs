namespace Dayswork.Core.Persistence.Dto;

public sealed class FishPondWorkScopeDtoV1
{
    public List<FishPondRefDtoV1> Ponds { get; set; } = new();
    public DestinationDtoV1? OutputDestination { get; set; }
}

public sealed class FishPondRefDtoV1
{
    public string LocationName { get; set; } = "";
    public int X { get; set; }
    public int Y { get; set; }
}
