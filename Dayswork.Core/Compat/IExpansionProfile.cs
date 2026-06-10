using Dayswork.Core.Domain;

namespace Dayswork.Core.Compat;

/// <summary>
/// Immutable description of one expansion's compatibility data plus the pure lookups over it.
/// The Vanilla profile is a Null-Object — every lookup reports "no override" so consumers keep
/// their existing behavior. Concrete expansions (e.g., SVE) override only
/// where their content differs from vanilla. Implementations hold no live game objects and operate
/// solely on primitive descriptors, so they are unit/property-testable without SMAPI.
/// </summary>
public interface IExpansionProfile
{
    /// <summary>Stable profile id (e.g., "vanilla", "sve").</summary>
    string Id { get; }

    /// <summary>True when this profile should be active given the set of installed mod ids.</summary>
    bool Matches(IReadOnlySet<string> installedModIds);

    /// <summary>
    /// Per-map worker-entrance override. Returns false when no override applies (the caller then
    /// uses the existing <c>Farm.warps</c> heuristic).
    /// </summary>
    bool TryGetEntranceOverride(FarmMapSignature signature, out TileCoord tile);

    /// <summary>Expansion locations that can participate in work selection or output routing.</summary>
    IReadOnlyList<ExpansionLocationDescriptor> GetLocationDescriptors();

    /// <summary>
    /// Explicit route lookup for supported cross-location expansion paths. Returns false when the
    /// profile has no exact route for the farm signature, source, target, and purpose.
    /// </summary>
    bool TryGetRoute(ExpansionRouteRequest request, out ExpansionRouteDefinition route);

    /// <summary>
    /// Maps an expansion premium animal-building type to its nearest vanilla
    /// <see cref="AnimalBuildingTier"/> for scope/pricing; returns null when not applicable.
    /// </summary>
    AnimalBuildingTier? MapPremiumBuildingTier(string buildingType);
}
