namespace Dayswork.Core.Domain;

public sealed record ContractScopeSelection(
    IReadOnlyList<Zone> OutdoorZones,
    IReadOnlyList<AnimalBuildingSelection> AnimalBuildings,
    GreenhouseSelection? Greenhouse);
