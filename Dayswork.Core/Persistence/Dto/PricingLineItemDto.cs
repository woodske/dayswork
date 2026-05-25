namespace Dayswork.Core.Persistence.Dto;

public sealed class PricingLineItemDto
{
    public string Family { get; set; } = "";
    public string Service { get; set; } = "";
    public int Quantity { get; set; }
    public int UnitPrice { get; set; }
    public int LineTotal { get; set; }
    public string? OutdoorBand { get; set; }
    public string? AnimalTier { get; set; }
}
