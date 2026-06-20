using Dayswork.Core.Crops;
using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData.Crops;
using StardewValley.GameData.Shops;
using CoreSeason = Dayswork.Core.Domain.Season;

namespace Dayswork.Integration;

/// <summary>
/// Live-game adapter for the Manage Crops authoring UI. Reads 1.6 crop, object, and shop
/// data, maps it into pure <see cref="CropCatalogSource"/> records, and delegates season-filtering /
/// supply-tagging / ordering to the pure <see cref="CropCatalog"/>. Catalog data is read once
/// per authoring session and memoized per season context; unmappable records are
/// skipped rather than thrown.
/// </summary>
internal sealed class CropCatalogProvider
{
    private const int FertilizerCategory = -19;
    private const string PierreShopId = "SeedShop";
    private const string JojaShopId = "Joja";

    // Fertilizers that do not apply to HoeDirt crops and must not appear in the crop fertilizer list.
    // Tree Fertilizer (O)805 only affects wild/fruit trees.
    private static readonly HashSet<string> ExcludedFertilizerIds = new(StringComparer.Ordinal) { GameItemIds.TreeFertilizer };

    private readonly IMonitor? _monitor;

    private IReadOnlyList<CropCatalogSource>? _sources;
    private IReadOnlyList<FertilizerOption>? _fertilizers;
    private readonly Dictionary<(CoreSeason? Season, bool Greenhouse), IReadOnlyList<CropCatalogEntry>> _catalogCache = new();

    public CropCatalogProvider(IMonitor? monitor = null) => _monitor = monitor;

    /// <summary>Season-filtered, supply-tagged, sorted crop list for the picker.</summary>
    public IReadOnlyList<CropCatalogEntry> GetCatalog(CoreSeason? seasonFilter, bool greenhouse)
    {
        var key = (greenhouse ? (CoreSeason?)null : seasonFilter, greenhouse);
        if (_catalogCache.TryGetValue(key, out var cached))
            return cached;

        var built = CropCatalog.Build(EnsureSources(), seasonFilter, greenhouse);
        _catalogCache[key] = built;
        return built;
    }

    /// <summary>The fertilizer options (plus the implicit "None" handled by the menu).</summary>
    public IReadOnlyList<FertilizerOption> GetFertilizers() => _fertilizers ??= BuildFertilizers();

    private IReadOnlyList<CropCatalogSource> EnsureSources() => _sources ??= BuildSources();

    private IReadOnlyList<CropCatalogSource> BuildSources()
    {
        var pierre = ReadShopItemIds(PierreShopId);
        var joja = ReadShopItemIds(JojaShopId);
        var result = new List<CropCatalogSource>();

        Dictionary<string, CropData> cropData;
        try
        {
            cropData = DataLoader.Crops(Game1.content);
        }
        catch (Exception ex)
        {
            _monitor?.Log($"Manage Crops catalog: could not load crop data ({ex.Message}).", LogLevel.Trace);
            return result;
        }

        foreach (var (seedId, data) in cropData)
        {
            var descriptor = TryMapCrop(seedId, data);
            if (descriptor is null)
                continue;

            // Wild seed crops (SpriteIndex 23) use the seed item's name ("Spring Seeds"),
            // not the forage placeholder stored in HarvestItemId ("Wild Horseradish").
            var displaySourceId = data.SpriteIndex == 23 ? seedId : data.HarvestItemId;
            var displayName = ResolveDisplayName(displaySourceId, fallback: seedId);
            result.Add(new CropCatalogSource(
                descriptor,
                displayName,
                StockedAtPierre: IsStocked(pierre, seedId),
                StockedAtJoja: IsStocked(joja, seedId)));
        }

        return result;
    }

    private CropDescriptor? TryMapCrop(string seedId, CropData data)
    {
        if (string.IsNullOrWhiteSpace(seedId))
            return null;

        // Wild seed crops (Spring/Summer/Fall/Winter Seeds) are identified by SpriteIndex 23 —
        // same as Crop.isWildSeedCrop(). Their HarvestItemId is a forage placeholder, not the real
        // harvest (determined at runtime via replaceWithObjectOnFullGrown). Use the seed ID as
        // CropItemId so they appear in the picker and shift log under their own name.
        bool isWildSeed = data.SpriteIndex == 23;
        var cropItemId = isWildSeed ? seedId : data.HarvestItemId;

        if (string.IsNullOrWhiteSpace(cropItemId))
            return null;

        var seasons = (data.Seasons ?? new List<Season>())
            .Select(MapSeason)
            .Where(season => season is not null)
            .Select(season => season!.Value)
            .Distinct()
            .ToList();

        if (seasons.Count == 0)
            return null;

        var daysToFirstHarvest = (data.DaysInPhase ?? new List<int>()).Where(days => days > 0).Sum();
        int? regrow = data.RegrowDays > 0 ? data.RegrowDays : null;

        return new CropDescriptor(
            cropItemId,
            seedId,
            fertilizerItemId: null,
            daysToFirstHarvest: daysToFirstHarvest,
            fertilizedDaysToFirstHarvest: null,
            regrowDays: regrow,
            seasons: seasons);
    }

    private IReadOnlyList<FertilizerOption> BuildFertilizers()
    {
        var pierre = ReadShopItemIds(PierreShopId);
        var joja = ReadShopItemIds(JojaShopId);
        var options = new List<FertilizerOption>();

        try
        {
            foreach (var (objectId, data) in Game1.objectData)
            {
                if (data.Category != FertilizerCategory || ExcludedFertilizerIds.Contains($"(O){objectId}"))
                    continue;

                var stocked = IsStocked(pierre, objectId) || IsStocked(joja, objectId);
                options.Add(new FertilizerOption(
                    objectId,
                    ResolveDisplayName(objectId, fallback: objectId),
                    stocked ? CropSupplyTag.AutoBuyable : CropSupplyTag.ChestSupplyOnly));
            }
        }
        catch (Exception ex)
        {
            _monitor?.Log($"Manage Crops catalog: could not load fertilizer data ({ex.Message}).", LogLevel.Trace);
        }

        return CropCatalog.SortFertilizers(options);
    }

    private HashSet<string> ReadShopItemIds(string shopId)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        try
        {
            var shops = DataLoader.Shops(Game1.content);
            if (!shops.TryGetValue(shopId, out var shop) || shop.Items is null)
                return ids;

            foreach (var item in shop.Items)
            {
                AddNormalizedId(ids, item.ItemId);
                if (item.RandomItemId is null)
                    continue;

                foreach (var randomId in item.RandomItemId)
                    AddNormalizedId(ids, randomId);
            }
        }
        catch (Exception ex)
        {
            _monitor?.Log($"Manage Crops catalog: could not load shop '{shopId}' ({ex.Message}).", LogLevel.Trace);
        }

        return ids;
    }

    private static void AddNormalizedId(HashSet<string> ids, string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return;

        ids.Add(id);
        ids.Add(StripQualifier(id));
    }

    private static bool IsStocked(HashSet<string> shopIds, string itemId) =>
        shopIds.Contains(itemId) || shopIds.Contains(StripQualifier(itemId));

    private static string StripQualifier(string id)
    {
        var close = id.IndexOf(')');
        return id.StartsWith("(", StringComparison.Ordinal) && close >= 0 ? id[(close + 1)..] : id;
    }

    private static string ResolveDisplayName(string itemId, string fallback)
    {
        try
        {
            var qualified = ItemRegistry.QualifyItemId(itemId) ?? itemId;
            var data = ItemRegistry.GetDataOrErrorItem(qualified);
            return string.IsNullOrWhiteSpace(data.DisplayName) ? fallback : data.DisplayName;
        }
        catch
        {
            return fallback;
        }
    }

    private static CoreSeason? MapSeason(Season season) => season switch
    {
        Season.Spring => CoreSeason.Spring,
        Season.Summer => CoreSeason.Summer,
        Season.Fall => CoreSeason.Fall,
        Season.Winter => CoreSeason.Winter,
        _ => null,
    };
}
