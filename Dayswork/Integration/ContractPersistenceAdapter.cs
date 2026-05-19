using Dayswork.Core.Persistence;
using Dayswork.Core.Persistence.Dto;
using Newtonsoft.Json;
using StardewModdingAPI;
using StardewModdingAPI.Events;

namespace Dayswork.Integration;

// Bridges SMAPI's save-data API with IContractStore + ISaveDataSerializer (M-15).
// NFR-SAFE-03: ReadSaveData returns null on first load; Deserialize(null) yields an
// empty list (established in U-06). WriteSaveData writes alongside the save — never
// touches the vanilla save file.
internal sealed class ContractPersistenceAdapter
{
    private const string SaveKey = "Dayswork.Contracts";

    private readonly IContractStore      _store;
    private readonly ISaveDataSerializer _serializer;
    private readonly IDataHelper         _dataHelper;
    private readonly string              _modVersion;

    public ContractPersistenceAdapter(
        IContractStore store,
        ISaveDataSerializer serializer,
        IDataHelper dataHelper,
        string modVersion)
    {
        _store      = store;
        _serializer = serializer;
        _dataHelper = dataHelper;
        _modVersion = modVersion;
    }

    public void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
    {
        var dto  = _dataHelper.ReadSaveData<DaysworkSaveDataV1>(SaveKey);
        var json = dto is null ? null : JsonConvert.SerializeObject(dto);
        var contracts = _serializer.Deserialize(json);
        _store.Hydrate(contracts);
    }

    public void OnSaving(object? sender, SavingEventArgs e)
    {
        var contracts = _store.List();
        var json = _serializer.Serialize(contracts, _modVersion);
        var dto  = JsonConvert.DeserializeObject<DaysworkSaveDataV1>(json);
        _dataHelper.WriteSaveData(SaveKey, dto);
    }
}
