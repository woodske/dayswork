namespace Dayswork.Tests.ManageCrops;

using Dayswork.Core.Crops;
using Xunit;

public sealed class StoreResolverTests
{
    [Fact]
    public void Resolve_FestivalDay_ReturnsNoStore()
    {
        var resolver = new StoreResolver();

        var result = resolver.Resolve(StorePreference.Either, "seed.parsnip", true, OpenStock());

        Assert.Null(result.Store);
        Assert.Equal(StoreClosedReason.Festival, result.ClosedReason);
    }

    [Fact]
    public void Resolve_Either_UsesPierreBeforeJoja()
    {
        var resolver = new StoreResolver();

        var result = resolver.Resolve(StorePreference.Either, "seed.parsnip", false, OpenStock());

        Assert.Equal(Store.Pierre, result.Store);
    }

    [Fact]
    public void Resolve_PreferredClosed_UsesFallback()
    {
        var resolver = new StoreResolver();
        var stock = new[]
        {
            new ShopStockSnapshot(Store.Pierre, false, new Dictionary<string, int> { ["seed.parsnip"] = 1 }),
            new ShopStockSnapshot(Store.Joja, true, new Dictionary<string, int> { ["seed.parsnip"] = 1 }),
        };

        var result = resolver.Resolve(StorePreference.Pierre, "seed.parsnip", false, stock);

        Assert.Equal(Store.Joja, result.Store);
        Assert.True(result.UsingFallback);
    }

    private static IReadOnlyList<ShopStockSnapshot> OpenStock() =>
        new[]
        {
            new ShopStockSnapshot(Store.Pierre, true, new Dictionary<string, int> { ["seed.parsnip"] = 1 }),
            new ShopStockSnapshot(Store.Joja, true, new Dictionary<string, int> { ["seed.parsnip"] = 1 }),
        };
}
