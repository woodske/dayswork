using Dayswork.Core.Persistence;
using Dayswork.Guards;
using Newtonsoft.Json.Linq;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace Dayswork.Integration;

/// <summary>
/// Farmhand upgrades in host save data, keyed by owner (2.0 plan D9). Save data is the host's
/// alone — SMAPI refuses <c>WriteSaveData</c> off the main player — so both hooks are host-gated
/// and a client learns its own entry from the menu snapshot instead.
/// </summary>
internal sealed class FarmhandUpgradePersistenceAdapter
{
    private const string SaveKey = "Dayswork.FarmhandUpgrades";

    private readonly FarmhandUpgradeStore _store;
    private readonly FarmhandUpgradeSaveDataSerializer _serializer;
    private readonly IDataHelper _dataHelper;

    public FarmhandUpgradePersistenceAdapter(
        FarmhandUpgradeStore store,
        FarmhandUpgradeSaveDataSerializer serializer,
        IDataHelper dataHelper)
    {
        _store = store;
        _serializer = serializer;
        _dataHelper = dataHelper;
    }

    public void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
    {
        // A split-screen guest shares this store with the host. Their per-screen SaveLoaded must
        // leave it alone; a genuinely remote client has its own process-local store and clears it
        // because its upgrade state comes from menu snapshots instead.
        if (Authority.IsSplitScreenGuest)
            return;

        var payload = Authority.IsHost ? _dataHelper.ReadSaveData<JToken>(SaveKey) : null;
        ApplyLoadedState(Authority.IsHost, isSplitScreenGuest: false, payload, Game1.MasterPlayer.UniqueMultiplayerID);
    }

    internal void ApplyLoadedState(bool isHostScreen, bool isSplitScreenGuest, JToken? payload, long hostId)
    {
        if (isSplitScreenGuest)
            return;

        if (!isHostScreen)
        {
            _store.Clear();
            return;
        }

        var json = payload?.ToString(Newtonsoft.Json.Formatting.None);

        // A pre-2.0 save held one global set of upgrades; the only player who could have bought
        // them is the host, so that is who they migrate to.
        _store.Hydrate(_serializer.Deserialize(json, hostId));
    }

    public void OnSaving(object? sender, SavingEventArgs e)
    {
        if (!Authority.IsHost)
            return;

        var json = _serializer.Serialize(_store.All);
        _dataHelper.WriteSaveData(SaveKey, JToken.Parse(json));
    }
}
