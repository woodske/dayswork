using Dayswork.Core.Persistence;
using Newtonsoft.Json.Linq;
using StardewModdingAPI;
using StardewModdingAPI.Events;

namespace Dayswork.Integration;

// Bridges SMAPI's save-data API with ContractStore + SaveDataSerializer (M-15).
// The adapter now passes schema-agnostic raw JSON payloads so serializer-owned
// version branching can handle both legacy v1 drop behavior and current v2 loads.
internal sealed class ContractPersistenceAdapter
{
    private const string SaveKey = "Dayswork.Contracts";

    private readonly ContractStore _store;
    private readonly SaveDataSerializer _serializer;
    private readonly IDataHelper _dataHelper;
    private readonly string _modVersion;

    public ContractPersistenceAdapter(
        ContractStore store,
        SaveDataSerializer serializer,
        IDataHelper dataHelper,
        string modVersion)
    {
        _store = store;
        _serializer = serializer;
        _dataHelper = dataHelper;
        _modVersion = modVersion;
    }

    public void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
    {
        var payload = _dataHelper.ReadSaveData<JToken>(SaveKey);
        var json = payload?.ToString(Newtonsoft.Json.Formatting.None);
        var contracts = _serializer.Deserialize(json);
        _store.Hydrate(contracts);
    }

    public void OnSaving(object? sender, SavingEventArgs e)
    {
        var contracts = _store.List();
        var json = _serializer.Serialize(contracts, _modVersion);
        var payload = JToken.Parse(json);
        _dataHelper.WriteSaveData(SaveKey, payload);
    }
}
