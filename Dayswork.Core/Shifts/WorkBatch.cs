using Dayswork.Core.Domain;

namespace Dayswork.Core.Shifts;

public enum BatchKind
{
    AnimalBuilding,

    // Per-building grazing pass (TODO-09): services the grazing animals belonging to the building
    // named by WorkBatch.LocationName. (Previously a single farm-wide pass for all buildings.)
    OutdoorAnimals,

    // Single farm-wide ground-forage sweep (truffles) that runs once after all building visits.
    FarmForage,

    Greenhouse,
    OutdoorCrops,
    OutdoorClearing,
}

public enum AnimalProductKind
{
    FloorForage,
    ToolHarvest,
    GroundForage,
}

public sealed record AnimalRef(long Id, string HomeLocation, string DisplayName);

public sealed record AnimalWorkItem(
    string LocationName,
    AnimalRef Animal,
    TaskKind Task,
    OutputScopeProvenance Provenance);

public sealed record WorkBatch(
    string LocationName,
    BatchKind Kind,
    IReadOnlyList<TaskKind> Tasks,
    IReadOnlyList<WorkItem> TileWork,
    IReadOnlyList<AnimalWorkItem> AnimalWork,
    bool FeedBuilding);
