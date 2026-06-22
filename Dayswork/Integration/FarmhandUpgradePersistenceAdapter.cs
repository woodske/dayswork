using Dayswork.Core.Persistence;
using Newtonsoft.Json.Linq;
using StardewModdingAPI;
using StardewModdingAPI.Events;

namespace Dayswork.Integration;

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
        var payload = _dataHelper.ReadSaveData<JToken>(SaveKey);
        var json = payload?.ToString(Newtonsoft.Json.Formatting.None);
        _store.Hydrate(_serializer.Deserialize(json));
    }

    public void OnSaving(object? sender, SavingEventArgs e)
    {
        var json = _serializer.Serialize(_store.State);
        _dataHelper.WriteSaveData(SaveKey, JToken.Parse(json));
    }
}
