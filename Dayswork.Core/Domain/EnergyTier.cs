namespace Dayswork.Core.Domain;

/// <summary>
/// The block of worker energy (labor capacity) the player purchases for a contract day.
/// Price is fixed per tier in config; the energy value becomes the worker's daily capacity.
/// </summary>
public enum EnergyTier
{
    HalfDay,
    FullDay,
    Overtime,
}
