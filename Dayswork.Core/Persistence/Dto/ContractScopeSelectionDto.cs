namespace Dayswork.Core.Persistence.Dto;

public sealed class ContractScopeSelectionDto
{
    public List<ZoneDtoV1> OutdoorZones { get; set; } = new();
    public List<AnimalBuildingSelectionDto> AnimalBuildings { get; set; } = new();
    public GreenhouseSelectionDto? Greenhouse { get; set; }
}
