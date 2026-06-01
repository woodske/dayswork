namespace Dayswork.Core.Compat;

/// <summary>
/// Pure derivation of an animal building's feed capacity from its real data, replacing the legacy
/// hardcoded Deluxe=12 / Big=8 / else=4 ladder (BR-SVE-08/10, pattern P-SVE-06). Total and
/// deterministic: negative inputs are treated as zero, so it never throws.
/// </summary>
public sealed class AnimalBuildingCapacityPolicy
{
    /// <summary>
    /// Capacity = the number of real trough tiles, bounded above by the building's max occupants.
    /// Equivalent to clamping the trough count into <c>[0, MaxOccupants]</c>.
    /// </summary>
    public int DeriveCapacity(AnimalBuildingCapacityInputs inputs)
    {
        var troughs = Math.Max(0, inputs.TroughTileCount);
        var max = Math.Max(0, inputs.MaxOccupants);
        return Math.Min(troughs, max);
    }
}
