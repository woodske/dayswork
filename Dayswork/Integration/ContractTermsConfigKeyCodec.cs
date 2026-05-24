using Dayswork.Core.Domain;
using Dayswork.Core.Energy;

namespace Dayswork.Integration;

internal static class ContractTermsConfigKeyCodec
{
    public static string EncodeOutdoorBandKey(OutdoorBandSize band) => band.ToString();

    public static string EncodeOutdoorPriceKey(OutdoorPriceKey key) => $"{key.Service}|{key.Band}";

    public static string EncodeAnimalBuildingPriceKey(AnimalBuildingPriceKey key) => $"{key.Service}|{key.Tier}";

    public static string EncodeGreenhousePriceKey(GreenhousePriceKey key) => key.Service.ToString();

    public static string EncodeWorkActionKey(WorkActionKind key) => key.ToString();
}
