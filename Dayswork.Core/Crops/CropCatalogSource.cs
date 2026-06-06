namespace Dayswork.Core.Crops;

/// <summary>
/// Raw, adapter-supplied catalog record for one crop, before season filtering / tagging.
/// The live-game adapter (<c>CropCatalogProvider</c>) maps live 1.6 crop + shop data into these
/// records; the pure <see cref="CropCatalog"/> then filters, tags, and sorts them. Keeping this a
/// plain record keeps all catalog decision logic deterministic and testable in Core (Q3=A).
/// </summary>
public sealed record CropCatalogSource(
    CropDescriptor Crop,
    string DisplayName,
    bool StockedAtPierre,
    bool StockedAtJoja)
{
    /// <summary>True when the seed is stocked at any store, so the farmhand can buy it.</summary>
    public bool IsAutoBuyable => StockedAtPierre || StockedAtJoja;
}
