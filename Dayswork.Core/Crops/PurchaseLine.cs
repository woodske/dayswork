namespace Dayswork.Core.Crops;

public sealed record PurchaseLine(Store Store, string ItemId, int Quantity)
{
    public int Quantity { get; init; } = Math.Max(0, Quantity);
}
