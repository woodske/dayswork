using Dayswork.Core.Domain;

namespace Dayswork.Core.Shifts;

/// <summary>
/// Travel anchors for within-category nearest-neighbor batch ordering. Resolved by the orchestrator
/// at shift start (game-side door/entry-tile lookups) and handed to the pure planner, so
/// <see cref="ShiftPlanBuilder"/> stays free of Stardew types.
///
/// <para><see cref="Anchors"/> maps a batch location name to a representative farm tile — a
/// building's outdoor door tile, or a standalone location's farm-side warp tile. A location absent
/// from the map (farm-wide "Farm" batches, or an expansion whose route didn't resolve) degrades to
/// name ordering, sorted after the anchored batches. <see cref="StartAnchor"/> is the worker's spawn
/// tile (office / farm entrance); each category's chain starts from it.</para>
///
/// <para>When no context is supplied to the planner, batch order falls back to today's alphabetical
/// ordering — deterministic degradation that keeps the planner's existing behaviour intact.</para>
/// </summary>
public sealed record BatchOrderingContext(
    IReadOnlyDictionary<string, TileCoord> Anchors,
    TileCoord StartAnchor);
