namespace Dayswork.Core.Crops;

using Dayswork.Core.Domain;

/// <summary>
/// Pure catalog logic for the Manage Crops authoring UI. Given adapter-supplied
/// <see cref="CropCatalogSource"/> records it produces the deterministic, season-filtered,
/// supply-tagged, sorted list of <see cref="CropCatalogEntry"/> rows the picker renders. No live
/// game state is touched here, so the filtering / tagging / ordering is fully testable.
/// </summary>
public static class CropCatalog
{
    /// <summary>
    /// Builds the pickable crop list for a season context.
    /// </summary>
    /// <param name="sources">Adapter-supplied crop records (vanilla + modded).</param>
    /// <param name="seasonFilter">
    /// On the open farm, the season to filter to (only crops growable that season are returned).
    /// Ignored when <paramref name="greenhouse"/> is true.
    /// </param>
    /// <param name="greenhouse">
    /// When true (greenhouse / Grandpa's Shed), no season filtering is applied — every crop is
    /// returned (season-agnostic).
    /// </param>
    public static IReadOnlyList<CropCatalogEntry> Build(
        IEnumerable<CropCatalogSource> sources,
        Season? seasonFilter,
        bool greenhouse)
    {
        if (sources is null)
            return Array.Empty<CropCatalogEntry>();

        var filtered = sources.Where(source => source is not null);

        if (!greenhouse && seasonFilter is { } season)
            filtered = filtered.Where(source => source.Crop.Seasons.Contains(season));

        return filtered
            .GroupBy(source => source.Crop.SeedItemId, StringComparer.Ordinal)
            .Select(group => group.First())
            .Select(source => new CropCatalogEntry(
                source.Crop,
                source.DisplayName,
                source.IsAutoBuyable ? CropSupplyTag.AutoBuyable : CropSupplyTag.ChestSupplyOnly))
            .OrderBy(entry => entry.DisplayName, StringComparer.Ordinal)
            .ThenBy(entry => entry.Crop.SeedItemId, StringComparer.Ordinal)
            .ToList()
            .AsReadOnly();
    }

    /// <summary>Deterministically de-duplicates and orders fertilizer options for the picker.</summary>
    public static IReadOnlyList<FertilizerOption> SortFertilizers(IEnumerable<FertilizerOption> options)
    {
        if (options is null)
            return Array.Empty<FertilizerOption>();

        return options
            .Where(option => option is not null)
            .GroupBy(option => option.ItemId, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(option => option.DisplayName, StringComparer.Ordinal)
            .ThenBy(option => option.ItemId, StringComparer.Ordinal)
            .ToList()
            .AsReadOnly();
    }
}
