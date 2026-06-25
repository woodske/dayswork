namespace Dayswork.Core.FishPonds;

using Dayswork.Core.Domain;

/// <summary>
/// One player-selected fish pond, identified by its location and the building's top-left tile
/// (<c>tileX</c>/<c>tileY</c>). Unlike a machine, a pond is a <c>Building</c> with no qualified item
/// id, so identity is purely <c>(location, tile)</c>. Re-resolved at shift start: if no fish pond
/// occupies that tile anymore (moved/demolished), the pond is skipped (dev log) — same contract as
/// machine tiles and managed-crop tiles.
/// </summary>
public sealed record FishPondRef(string LocationName, TileCoord Tile);
