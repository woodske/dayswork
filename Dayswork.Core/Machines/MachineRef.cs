namespace Dayswork.Core.Machines;

using Dayswork.Core.Domain;

/// <summary>
/// One player-selected machine, identified by its location, tile, and the qualified id it was
/// when selected. Re-resolved at shift start: if the tile is empty, holds a different machine, or
/// the location is gone, the machine is skipped (dev log) — same contract as managed-crop tiles.
/// </summary>
public sealed record MachineRef(string LocationName, TileCoord Tile, string ExpectedQualifiedId);
