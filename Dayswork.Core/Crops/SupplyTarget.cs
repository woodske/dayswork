namespace Dayswork.Core.Crops;

public sealed record SupplyTarget(string ItemId, int Quantity, StorePreference StorePreference)
{
    public int Quantity { get; init; } = Math.Max(0, Quantity);
}
