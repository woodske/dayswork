namespace Dayswork.Core.Domain;

public sealed record WorkScopeSet(
    OutdoorWorkScope? OutdoorWork,
    IReadOnlyList<AnimalBuildingScope> AnimalBuildings,
    GreenhouseWorkScope? GreenhouseWork);
