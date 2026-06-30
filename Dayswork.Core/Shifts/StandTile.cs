using Dayswork.Core.Domain;

namespace Dayswork.Core.Shifts;

/// <summary>
/// A candidate tile the worker can stand on to act on a target, tagged with whether it sits
/// <paramref name="Diagonal"/>ly (vs orthogonally) to that target. The selector adds a small
/// travel-cost bias to diagonal tiles so the worker prefers an orthogonal stand unless a diagonal
/// is meaningfully closer (see <see cref="WorkerRouteSelector.TrySelectPreferredStandTile"/>).
/// </summary>
public readonly record struct StandTile(TileCoord Tile, bool Diagonal);
