using Dayswork.Core.Domain;

namespace Dayswork.Core.Shifts;

public sealed record WorkItem(TileCoord Tile, TaskKind Task);
