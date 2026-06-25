using Dayswork.Core.Domain;

namespace Dayswork.Core.Shifts;

public enum BatchKind
{
    AnimalBuilding,

    // Per-building grazing pass: services the grazing animals belonging to the building
    // named by WorkBatch.LocationName. (Previously a single farm-wide pass for all buildings.)
    OutdoorAnimals,

    // Single farm-wide ground-forage sweep (truffles) that runs once after all building visits.
    FarmForage,

    Greenhouse,
    OutdoorCrops,
    OutdoorClearing,

    // Managed crops: a contract's authored crop zones, prepared/planted/maintained
    // per-tile from the pure CropShiftPlanner. The batch's CropZoneAssignments are carried
    // out-of-band by the runtime (not as TaskKind TileWork).
    ManagedCrops,

    // Manage Machines: the contract's selected machines in this location, visited to collect
    // finished output and reload empty machines. The machine refs/groups are carried out-of-band
    // by the runtime (Tasks empty), like managed crops.
    Machines,

    // Harvest Cave: visit the FarmCave location and collect bat fruit or mushroom-box output.
    // No scope selection; the cave is always a single static location.
    FarmCave,

    // Manage Fish Ponds: the contract's selected fish ponds in this location, visited to collect
    // finished pond output. Collect-only (the player stocks the fish). The pond refs are carried
    // out-of-band by the runtime (Tasks empty), like managed crops and machines.
    FishPonds,
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
