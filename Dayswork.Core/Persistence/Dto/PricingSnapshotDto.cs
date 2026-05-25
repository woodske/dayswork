namespace Dayswork.Core.Persistence.Dto;

public sealed class PricingSnapshotDto
{
    public List<PricingLineItemDto> LineItems { get; set; } = new();
    public int OutdoorSubtotal { get; set; }
    public int AnimalSubtotal { get; set; }
    public int GreenhouseSubtotal { get; set; }
    public int TotalPrice { get; set; }
}
