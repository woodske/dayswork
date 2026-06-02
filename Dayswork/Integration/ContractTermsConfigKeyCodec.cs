using Dayswork.Core.Domain;
using Dayswork.Core.Energy;

namespace Dayswork.Integration;

internal static class ContractTermsConfigKeyCodec
{
    public static string EncodeEnergyTierKey(EnergyTier tier) => tier.ToString();

    public static string EncodeWorkActionKey(WorkActionKind key) => key.ToString();
}
