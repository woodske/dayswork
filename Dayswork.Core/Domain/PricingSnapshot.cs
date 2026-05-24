namespace Dayswork.Core.Domain;

public sealed record PricingSnapshot(
    IReadOnlyList<PricingLineItem> LineItems,
    int OutdoorSubtotal,
    int AnimalSubtotal,
    int GreenhouseSubtotal,
    int TotalPrice);
