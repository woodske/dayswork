namespace Dayswork.Core.Crops;

using Dayswork.Core.Domain;

public sealed record TileState(
    TileCoord Tile,
    bool ReadyToHarvest,
    bool HasCrop,
    bool HasDebris,
    bool IsTilled,
    bool HasFertilizer,
    bool IsWatered)
{
    public bool CanAcceptSeed => !HasCrop && !HasDebris;
}
