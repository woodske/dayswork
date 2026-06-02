namespace Dayswork.Core.Domain;

/// <summary>
/// The fixed price the player pays for a contract day. Under the energy-tier model the price is the
/// purchased tier's configured price; there is no per-scope breakdown.
/// </summary>
public sealed record PricingSnapshot(int TotalPrice);
