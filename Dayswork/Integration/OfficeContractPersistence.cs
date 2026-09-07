using Dayswork.Core.Domain;
using Dayswork.Core.Persistence;
using Dayswork.Diagnostics;
using Newtonsoft.Json.Linq;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace Dayswork.Integration;

/// <summary>
/// Durable storage for contracts. Each office holds its own contract in its
/// <see cref="OfficeModData.ContractKey"/> modData (see that file for why); this class is the only
/// thing that reads or writes it, and it does so on <c>SaveLoaded</c> (hydrate) and on every
/// committed store mutation (write back exactly the one office that changed — never per tick).
///
/// The legacy <c>Dayswork.Contracts</c> save key is still read once, to adopt a 1.x contract onto
/// its office, and is then overwritten with a marker envelope so the adoption cannot run twice and
/// a 1.x mod opening the save loads nothing.
/// </summary>
internal sealed class OfficeContractPersistence
{
    private const string LegacySaveKey = "Dayswork.Contracts";

    private readonly OfficeContractStore _store;
    private readonly SaveDataSerializer _serializer;
    private readonly IDataHelper _dataHelper;
    private readonly string _modVersion;
    private readonly IShiftOutcomeDispatcher _shiftOutcomes;

    public OfficeContractPersistence(
        OfficeContractStore store,
        SaveDataSerializer serializer,
        IDataHelper dataHelper,
        string modVersion,
        IShiftOutcomeDispatcher shiftOutcomes)
    {
        _store = store;
        _serializer = serializer;
        _dataHelper = dataHelper;
        _modVersion = modVersion;
        _shiftOutcomes = shiftOutcomes;

        _store.ContractCommitted += WriteBack;
    }

    public void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
    {
        var farm = Game1.getFarm();
        var hydrated = new List<KeyValuePair<Guid, Contract>>();

        foreach (var office in OfficeResolver.EnumerateOffices(farm))
        {
            var contract = _serializer.DeserializeOne(OfficeModData.ReadContractJson(office));
            if (contract is not null)
                hydrated.Add(new KeyValuePair<Guid, Contract>(office.id.Value, contract));
        }

        _store.HydrateAll(hydrated);

        AdoptLegacyContractIfAny();
    }

    /// <summary>
    /// Nothing to do on save: contracts are written the moment they change, and they live on
    /// buildings, which the game serializes itself. The legacy key is left holding its marker.
    /// </summary>
    public void OnSaving(object? sender, SavingEventArgs e)
    {
    }

    // ── 1.x adoption (2.0 plan D6) ───────────────────────────────────────────

    private void AdoptLegacyContractIfAny()
    {
        var payload = _dataHelper.ReadSaveData<JToken>(LegacySaveKey);
        var json = payload?.ToString(Newtonsoft.Json.Formatting.None);
        if (json is null || _serializer.IsMigrationMarker(json))
            return;

        var legacy = _serializer.Deserialize(json);
        var offices = OfficeResolver.EnumerateOffices(Game1.getFarm())
            .Select(office => new OfficeIdentity(office.id.Value, office.owner.Value))
            .ToList();

        var result = ContractAdoption.Adopt(legacy, offices, Game1.MasterPlayer.UniqueMultiplayerID);
        switch (result.Outcome)
        {
            case ContractAdoptionOutcome.Adopted when result.Adopted is { } adopted:
                // Straight into the store as a commit, so the write-back puts it on the building.
                // An office that somehow already holds a 2.0 contract keeps it — the adopted one
                // would be a duplicate of it.
                if (_store.ForOffice(adopted.OfficeId) is null)
                {
                    _store.Add(adopted);
                    ModEntry.ModMonitor.Log(
                        $"[Dayswork] Adopted the save's 1.x contract {adopted.Id.Value} onto office {adopted.OfficeId} (owner {adopted.OwnerId}).",
                        DevLog.WarnLevel);
                }
                break;

            case ContractAdoptionOutcome.DroppedNoOffice:
                // A 1.x contract had no owner field; the host is the only sensible addressee.
                _shiftOutcomes.ShowContractLostNotice(Game1.MasterPlayer.UniqueMultiplayerID);
                ModEntry.ModMonitor.Log(
                    "[Dayswork] The save's 1.x contract could not be adopted: the farm has no office. It has been dropped (no refund).",
                    DevLog.WarnLevel);
                break;

            case ContractAdoptionOutcome.DroppedAmbiguous:
                _shiftOutcomes.ShowContractLostNotice(Game1.MasterPlayer.UniqueMultiplayerID);
                ModEntry.ModMonitor.Log(
                    "[Dayswork] The save's 1.x contract could not be adopted: several offices exist and there is no way to tell which one it belonged to. It has been dropped (no refund).",
                    DevLog.WarnLevel);
                break;
        }

        // Mark the legacy key migrated whatever the outcome — a contract that could not be adopted
        // must not be re-offered tomorrow.
        _dataHelper.WriteSaveData(LegacySaveKey, JToken.Parse(_serializer.SerializeMigrationMarker(_modVersion)));
    }

    // ── Write-back ───────────────────────────────────────────────────────────

    private void WriteBack(Guid officeId, Contract? contract)
    {
        var office = OfficeResolver.TryGet(officeId);
        if (office is null)
        {
            // The building went away between the mutation and this callback (demolition races the
            // contract edit). The contract is gone with it, which is the intended outcome.
            DevLog.Log($"[Dayswork] Skipped a contract write-back: office {officeId} no longer exists.");
            return;
        }

        OfficeModData.WriteContractJson(
            office,
            contract is null ? null : _serializer.SerializeOne(contract, _modVersion));
    }
}
