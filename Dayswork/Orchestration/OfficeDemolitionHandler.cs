using Dayswork.Core.Persistence;
using Dayswork.Integration;
using StardewModdingAPI.Events;
using StardewValley;

namespace Dayswork.Orchestration;

/// <summary>
/// An office removed from the farm takes its contract with it. The contract is the building's, so
/// there is nothing to reconcile — but a live shift working out of that office has to be stopped,
/// and the player has to be told, because an unexecuted one-time contract's price is forfeited
/// (2.0 plan D1: demolition is player-initiated, and a refund path is more code than the case
/// deserves).
///
/// Ending the shift goes through the normal early-stop path, so hard rule 4 still holds: the worker
/// deposits what it is carrying, and overflow that would have gone to the now-missing office chest
/// falls through to the shipping bin.
/// </summary>
internal sealed class OfficeDemolitionHandler
{
    private readonly OfficeContractStore _store;
    private readonly ShiftFleet _fleet;
    private readonly IShiftOutcomeDispatcher _shiftOutcomes;

    public OfficeDemolitionHandler(
        OfficeContractStore store,
        ShiftFleet fleet,
        IShiftOutcomeDispatcher shiftOutcomes)
    {
        _store = store;
        _fleet = fleet;
        _shiftOutcomes = shiftOutcomes;
    }

    public void OnBuildingListChanged(object? sender, BuildingListChangedEventArgs e)
    {
        if (e.Location is not Farm)
            return;

        foreach (var building in e.Removed)
        {
            if (!OfficeResolver.IsOffice(building))
                continue;

            var officeId = building.id.Value;
            var contract = _store.ForOffice(officeId);

            _fleet.ForOffice(officeId)?.EndShiftEarly();
            _store.RemoveOffice(officeId);

            if (contract is null)
                continue;

            _shiftOutcomes.ShowContractLostNotice(contract.OwnerId);
            ModEntry.ModMonitor.Log(
                $"[Dayswork] Office {officeId:N} was demolished — contract {contract.Id.Value} is gone with it (no refund).",
                DevLog.WarnLevel);
        }
    }
}
