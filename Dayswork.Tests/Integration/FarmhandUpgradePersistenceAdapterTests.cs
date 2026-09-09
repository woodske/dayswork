using Dayswork.Core.Persistence;
using Dayswork.Core.Upgrades;
using Dayswork.Integration;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Dayswork.Tests.Integration;

public sealed class FarmhandUpgradePersistenceAdapterTests
{
    private const long HostId = 100;
    private const long GuestId = 200;

    [Fact]
    public void Dayswork2Review_R1_GuestJoinThenHostSaveAndReloadPreservesPurchasedUpgrades()
    {
        var serializer = new FarmhandUpgradeSaveDataSerializer(_ => { });
        var liveStore = new FarmhandUpgradeStore();
        liveStore.Replace(HostId, FarmhandUpgradeState.Empty.MarkPurchased(FarmhandUpgradeKind.Speed));
        liveStore.Replace(GuestId, FarmhandUpgradeState.Empty.MarkPurchased(FarmhandUpgradeKind.Energy));
        var liveAdapter = new FarmhandUpgradePersistenceAdapter(liveStore, serializer, null!);

        liveAdapter.ApplyLoadedState(
            isHostScreen: false,
            isSplitScreenGuest: true,
            payload: null,
            hostId: HostId);
        var payloadWrittenByNextHostSave = JToken.Parse(serializer.Serialize(liveStore.All));

        var reloadedStore = new FarmhandUpgradeStore();
        var reloadedAdapter = new FarmhandUpgradePersistenceAdapter(reloadedStore, serializer, null!);
        reloadedAdapter.ApplyLoadedState(
            isHostScreen: true,
            isSplitScreenGuest: false,
            payloadWrittenByNextHostSave,
            HostId);

        Assert.True(reloadedStore.For(HostId).SpeedPurchased);
        Assert.True(reloadedStore.For(GuestId).EnergyPurchased);
    }

    [Fact]
    public void Dayswork2Review_R1_RemoteClientLoadClearsItsProcessLocalUpgradeStore()
    {
        var serializer = new FarmhandUpgradeSaveDataSerializer(_ => { });
        var store = new FarmhandUpgradeStore();
        store.Replace(HostId, FarmhandUpgradeState.Empty.MarkPurchased(FarmhandUpgradeKind.Speed));
        var sut = new FarmhandUpgradePersistenceAdapter(store, serializer, null!);

        sut.ApplyLoadedState(
            isHostScreen: false,
            isSplitScreenGuest: false,
            payload: null,
            hostId: HostId);

        Assert.Equal(FarmhandUpgradeState.Empty, store.For(HostId));
    }
}
