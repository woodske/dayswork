namespace Dayswork.Core.Domain;

public sealed record PricingLineItem(
    PricingFamily Family,
    TaskKind Service,
    int Quantity,
    int UnitPrice,
    int LineTotal,
    OutdoorBandSize? OutdoorBand,
    AnimalBuildingTier? AnimalTier);
