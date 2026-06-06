namespace Dayswork.Core.Crops;

/// <summary>
/// A pickable crop row in the Manage Crops authoring UI: the underlying crop identity plus
/// authoring-only display name and supply tagging. Produced by the pure <see cref="CropCatalog"/>.
/// </summary>
public sealed record CropCatalogEntry(
    CropDescriptor Crop,
    string DisplayName,
    CropSupplyTag Supply);
