namespace Dayswork.Core.Crops;

using Dayswork.Core.Domain;

public sealed record TileAction(
    string LocationName,
    TileCoord Tile,
    ManagedCropActionKind Kind,
    string? ItemId = null,
    bool RequiresDiggable = false);
