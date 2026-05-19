namespace Dayswork.Core.Domain;

public sealed record Zone(string LocationName, TileCoord TopLeft, TileCoord BottomRight);
