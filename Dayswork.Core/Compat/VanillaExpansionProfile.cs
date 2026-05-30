using Dayswork.Core.Domain;

namespace Dayswork.Core.Compat;

/// <summary>
/// Default profile representing "no expansion overrides". Every lookup returns "no override", so
/// all consumers take their existing vanilla code paths (NFR-SVE-01 / S-21 / pattern P-SVE-04).
/// Matches any installed-mod set, so it serves as the lowest-precedence fallback in the selector.
/// </summary>
public sealed class VanillaExpansionProfile : IExpansionProfile
{
    public string Id => "vanilla";

    public IReadOnlySet<string> FarmMapModIds { get; } = new HashSet<string>();

    public bool Matches(IReadOnlySet<string> installedModIds) => true;

    public bool TryGetEntranceOverride(FarmMapSignature signature, out TileCoord tile)
    {
        tile = default;
        return false;
    }

    public bool TryClassifyContentOverride(ContentDescriptor descriptor, out WorkClassification result)
    {
        result = WorkClassification.NoOverride;
        return false;
    }

    public bool IsExpansionWorkLocation(string locationName) => false;

    public AnimalBuildingTier? MapPremiumBuildingTier(string buildingType) => null;
}
