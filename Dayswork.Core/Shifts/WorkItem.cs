using Dayswork.Core.Domain;

namespace Dayswork.Core.Shifts;

/// <summary>
/// A single item of work for the worker to execute.
/// NavTile is where the worker walks to; TaskTile is where the action is performed.
/// For normal tiles these are the same. For trellis crops (FR-SKIP-04 / FD-Q4=B),
/// NavTile is the nearest reachable orthogonal neighbour and TaskTile is the crop tile.
/// </summary>
public sealed record WorkItem(TileCoord NavTile, TileCoord TaskTile, TaskKind Task);
