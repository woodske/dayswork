namespace Dayswork.Core.Domain;

public sealed record ContractPriceTotals(
    int OutdoorSubtotal,
    int AnimalSubtotal,
    int GreenhouseSubtotal,
    int TotalPrice);
