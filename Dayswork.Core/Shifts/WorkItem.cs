using Dayswork.Core.Domain;

namespace Dayswork.Core.Shifts;

/// <summary>
/// A single item of work for the worker to execute.
/// NavTile is where the worker walks to; TaskTile is where the action is performed.
/// For walkable targets these are the same. For trellis crops and blocked objects
/// like trees, rocks, and weeds, NavTile is a reachable orthogonal neighbour and
/// TaskTile is the tile the action targets.
/// </summary>
public sealed record WorkItem(TileCoord NavTile, TileCoord TaskTile, TaskKind Task);
