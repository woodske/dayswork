namespace Dayswork.Core.Persistence.Dto;

public sealed class ContractScopeSelectionDto
{
    public List<ZoneDtoV1> OutdoorZones { get; set; } = new();
    public List<AnimalBuildingSelectionDto> AnimalBuildings { get; set; } = new();

    // Legacy single-greenhouse field (pre-TODO-10 saves). Still read for backward compatibility;
    // new saves write the Greenhouses list below and leave this null.
    public GreenhouseSelectionDto? Greenhouse { get; set; }

    // All selected greenhouses (TODO-10). Null/empty on legacy saves — the reader falls back to the
    // single Greenhouse field above.
    public List<GreenhouseSelectionDto>? Greenhouses { get; set; }
}
