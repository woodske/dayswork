using Dayswork.Core.Domain;

namespace Dayswork.Core.Shifts;

public enum BatchKind
{
    AnimalBuilding,
    OutdoorAnimals,
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
