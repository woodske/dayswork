using Dayswork.Core.Domain;

namespace Dayswork.Core.Compat;

/// <summary>
/// Default profile representing "no expansion overrides". Every lookup returns "no override", so
/// all consumers take their existing vanilla code paths.
/// Matches any installed-mod set, so it serves as the lowest-precedence fallback in the selector.
/// </summary>
public sealed class VanillaExpansionProfile : IExpansionProfile
{
    public string Id => "vanilla";

    public bool Matches(IReadOnlySet<string> installedModIds) => true;

    public bool TryGetEntranceOverride(FarmMapSignature signature, out TileCoord tile)
    {
        tile = default;
        return false;
    }

    public IReadOnlyList<ExpansionLocationDescriptor> GetLocationDescriptors() =>
        Array.Empty<ExpansionLocationDescriptor>();

    public bool TryGetRoute(ExpansionRouteRequest request, out ExpansionRouteDefinition route)
    {
        route = null!;
        return false;
    }

    public AnimalBuildingTier? MapPremiumBuildingTier(string buildingType) => null;
}
