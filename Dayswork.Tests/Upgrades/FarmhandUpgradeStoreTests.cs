namespace Dayswork.Tests.Upgrades;

using Dayswork.Core.Persistence;
using Dayswork.Core.Upgrades;
using Xunit;

/// <summary>
/// Upgrades are per owner since 2.0 (plan D9). Single-player has exactly one owner, so the point of
/// these tests is the co-op case they make possible: one player's purchase must not speed up
/// another player's farmhand.
/// </summary>
public class FarmhandUpgradeStoreTests
{
    private const long Alice = 100L;
    private const long Bob = 200L;

    [Fact]
    public void AnUnknownOwnerHasNothingPurchased()
    {
        var store = new FarmhandUpgradeStore();

        Assert.Equal(FarmhandUpgradeState.Empty, store.For(Alice));
        Assert.False(store.IsPurchased(Alice, FarmhandUpgradeKind.Speed));
    }

    [Fact]
    public void OnePlayersPurchaseDoesNotReachAnother()
    {
        var store = new FarmhandUpgradeStore();

        store.Replace(Alice, FarmhandUpgradeState.Empty.MarkPurchased(FarmhandUpgradeKind.Speed));

        Assert.True(store.IsPurchased(Alice, FarmhandUpgradeKind.Speed));
        Assert.False(store.IsPurchased(Bob, FarmhandUpgradeKind.Speed));
    }

    [Fact]
    public void AllOmitsOwnersWhoHaveBoughtNothing()
    {
        // Persisting an empty entry would grow the save file with a row per player who has ever
        // opened the upgrades page.
        var store = new FarmhandUpgradeStore();
        store.Replace(Alice, FarmhandUpgradeState.Empty.MarkPurchased(FarmhandUpgradeKind.Energy));
        store.Replace(Bob, FarmhandUpgradeState.Empty);

        Assert.Single(store.All);
        Assert.True(store.All[Alice].EnergyPurchased);
    }

    [Fact]
    public void HydrateReplacesEverythingRatherThanMerging()
    {
        var store = new FarmhandUpgradeStore();
        store.Replace(Alice, FarmhandUpgradeState.Empty.MarkPurchased(FarmhandUpgradeKind.Speed));

        store.Hydrate(new Dictionary<long, FarmhandUpgradeState>
        {
            [Bob] = FarmhandUpgradeState.Empty.MarkPurchased(FarmhandUpgradeKind.Energy),
        });

        Assert.Equal(FarmhandUpgradeState.Empty, store.For(Alice));
        Assert.True(store.IsPurchased(Bob, FarmhandUpgradeKind.Energy));
    }

    [Fact]
    public void ClearEmptiesTheStore()
    {
        var store = new FarmhandUpgradeStore();
        store.Replace(Alice, FarmhandUpgradeState.Empty.MarkPurchased(FarmhandUpgradeKind.Speed));

        store.Clear();

        Assert.Empty(store.All);
    }
}
