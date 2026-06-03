using Dayswork.Core.Domain;

namespace Dayswork.Core.Inventory;

internal enum UndeliveredDepositResolution
{
    AutomaticOverflow,
    ShippingBin,
}

internal static class DepositFallbackPolicy
{
    public static UndeliveredDepositResolution ResolveUndelivered(DestinationKey destination) =>
        destination is ShippingBinDestination
            ? UndeliveredDepositResolution.ShippingBin
            : UndeliveredDepositResolution.AutomaticOverflow;
}
