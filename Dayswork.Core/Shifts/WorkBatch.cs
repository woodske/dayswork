using Dayswork.Core.Domain;

namespace Dayswork.Core.Shifts;

public enum BatchKind
{
    AnimalBuilding,
    Interior,
    OutdoorFarm,
}

public enum AnimalProductKind
{
    FloorForage,
    ToolHarvest,
    GroundForage,
}

public sealed record AnimalRef(long Id, string HomeLocation, string DisplayName);

public sealed record AnimalWorkItem(string LocationName, AnimalRef Animal, TaskKind Task);

public sealed record WorkBatch(
    string LocationName,
    BatchKind Kind,
    IReadOnlyList<WorkItem> TileWork,
    IReadOnlyList<AnimalWorkItem> AnimalWork,
    bool FeedBuilding);
