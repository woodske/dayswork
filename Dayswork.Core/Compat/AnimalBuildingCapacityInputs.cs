namespace Dayswork.Core.Compat;

/// <summary>
/// Pure inputs for animal-building feed-capacity derivation.
/// </summary>
/// <param name="TroughTileCount">Number of real "Trough" Back-layer tiles in the animal house.</param>
/// <param name="MaxOccupants">Building-data max occupants (upper bound on feed slots).</param>
public readonly record struct AnimalBuildingCapacityInputs(int TroughTileCount, int MaxOccupants);
