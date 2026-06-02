namespace Dayswork.Core.Domain;

public abstract record DestinationKey;

public sealed record ChestDestination(ChestRef Ref) : DestinationKey;

public sealed record ShippingBinDestination : DestinationKey
{
    public static readonly ShippingBinDestination Instance = new();
}

public sealed record AutomaticOutputDestination : DestinationKey
{
    public static readonly AutomaticOutputDestination Instance = new();
}
