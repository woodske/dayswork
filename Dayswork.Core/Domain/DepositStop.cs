namespace Dayswork.Core.Domain;

/// <summary>
/// A deposit destination's position for trip-ordering: its location plus its tile. The distance
/// metric needs both because a chest can live in a building interior (or another location entirely),
/// where a bare tile compares unrelated coordinate spaces. <see cref="LocationName"/> matches
/// <see cref="ChestRef.LocationName"/> — <c>farm.Name</c> ("Farm") for farm chests, the interior's
/// <c>NameOrUniqueName</c> otherwise; the shipping bin and the worker's start are farm-space by
/// construction.
/// </summary>
public readonly record struct DepositStop(string LocationName, TileCoord Tile);
