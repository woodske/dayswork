namespace Dayswork.Tests.ManageCrops;

using Dayswork.Core.Crops;
using Dayswork.Core.Domain;
using Xunit;

public sealed class CropCatalogTests
{
    private static CropDescriptor Crop(string id, params Season[] seasons) =>
        new($"crop.{id}", $"seed.{id}", null, 4, null, null, seasons);

    private static CropCatalogSource Source(string id, bool pierre, bool joja, params Season[] seasons) =>
        new(Crop(id, seasons), id, pierre, joja);

    [Fact]
    public void Build_OnOpenFarm_FiltersToSelectedSeason()
    {
        var sources = new[]
        {
            Source("parsnip", pierre: true, joja: false, Season.Spring),
            Source("corn", pierre: true, joja: false, Season.Summer, Season.Fall),
        };

        var spring = CropCatalog.Build(sources, Season.Spring, greenhouse: false);

        var only = Assert.Single(spring);
        Assert.Equal("seed.parsnip", only.Crop.SeedItemId);
    }

    [Fact]
    public void Build_Greenhouse_IgnoresSeasonFilter()
    {
        var sources = new[]
        {
            Source("parsnip", pierre: true, joja: false, Season.Spring),
            Source("corn", pierre: true, joja: false, Season.Summer, Season.Fall),
        };

        var all = CropCatalog.Build(sources, Season.Spring, greenhouse: true);

        Assert.Equal(2, all.Count);
    }

    [Fact]
    public void Build_GreenhouseWithNullSeason_ReturnsAllCropsForYearRoundPicker()
    {
        var sources = new[]
        {
            Source("parsnip", pierre: true, joja: false, Season.Spring),
            Source("pumpkin", pierre: true, joja: false, Season.Fall),
        };

        var all = CropCatalog.Build(sources, seasonFilter: null, greenhouse: true);

        Assert.Equal(new[] { "seed.parsnip", "seed.pumpkin" }, all.Select(entry => entry.Crop.SeedItemId).OrderBy(id => id));
    }

    [Fact]
    public void Build_TagsAutoBuyableWhenStockedAtEitherStore()
    {
        var sources = new[]
        {
            Source("parsnip", pierre: true, joja: false, Season.Spring),
            Source("jojaonly", pierre: false, joja: true, Season.Spring),
            Source("ancient", pierre: false, joja: false, Season.Spring),
        };

        var catalog = CropCatalog.Build(sources, Season.Spring, greenhouse: false);

        Assert.Equal(CropSupplyTag.AutoBuyable, Find(catalog, "seed.parsnip").Supply);
        Assert.Equal(CropSupplyTag.AutoBuyable, Find(catalog, "seed.jojaonly").Supply);
        Assert.Equal(CropSupplyTag.ChestSupplyOnly, Find(catalog, "seed.ancient").Supply);
    }

    [Fact]
    public void Build_SortsByDisplayNameThenSeedId()
    {
        var sources = new[]
        {
            new CropCatalogSource(Crop("b", Season.Spring), "Cauliflower", true, false),
            new CropCatalogSource(Crop("a", Season.Spring), "Bean", true, false),
        };

        var catalog = CropCatalog.Build(sources, Season.Spring, greenhouse: false);

        Assert.Equal("Bean", catalog[0].DisplayName);
        Assert.Equal("Cauliflower", catalog[1].DisplayName);
    }

    [Fact]
    public void Build_DeduplicatesBySeedId()
    {
        var sources = new[]
        {
            Source("parsnip", pierre: true, joja: false, Season.Spring),
            Source("parsnip", pierre: true, joja: false, Season.Spring),
        };

        Assert.Single(CropCatalog.Build(sources, Season.Spring, greenhouse: false));
    }

    [Fact]
    public void Build_NullSources_ReturnsEmpty()
    {
        Assert.Empty(CropCatalog.Build(null!, Season.Spring, greenhouse: false));
    }

    [Fact]
    public void SortFertilizers_DeduplicatesAndOrders()
    {
        var options = new[]
        {
            new FertilizerOption("fert.q", "Quality Fertilizer", CropSupplyTag.AutoBuyable),
            new FertilizerOption("fert.b", "Basic Fertilizer", CropSupplyTag.AutoBuyable),
            new FertilizerOption("fert.b", "Basic Fertilizer", CropSupplyTag.AutoBuyable),
        };

        var sorted = CropCatalog.SortFertilizers(options);

        Assert.Equal(2, sorted.Count);
        Assert.Equal("Basic Fertilizer", sorted[0].DisplayName);
        Assert.Equal("Quality Fertilizer", sorted[1].DisplayName);
    }

    private static CropCatalogEntry Find(IReadOnlyList<CropCatalogEntry> catalog, string seedId) =>
        catalog.Single(entry => entry.Crop.SeedItemId == seedId);
}
