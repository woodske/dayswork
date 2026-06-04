namespace Dayswork.Core.Crops;

public sealed record StoreResolution(
    Store? Store,
    bool UsingFallback,
    StoreClosedReason? ClosedReason,
    string? ItemId)
{
    public bool CanPurchase => Store is not null && ClosedReason is null;
}
