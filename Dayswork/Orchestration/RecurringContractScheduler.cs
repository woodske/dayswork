using Dayswork.Core.Domain;
using Dayswork.Core.Persistence;
using Dayswork.Guards;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace Dayswork.Orchestration;

internal sealed class RecurringContractScheduler
{
    private readonly IContractStore _store;
    private readonly ShiftOrchestrator _orchestrator;

    public RecurringContractScheduler(IContractStore store, ShiftOrchestrator orchestrator)
    {
        _store        = store;
        _orchestrator = orchestrator;
    }

    public void OnDayStarted(object? sender, DayStartedEventArgs e)
    {
        // REL-U10-01: multiplayer guard — no-op in multiplayer sessions.
        if (MultiplayerGuard.IsMultiplayer())
            return;

        var today = CurrentGameDate();
        var contractsForToday = _store.ListActiveForDate(today.Day, today.Season, today.Year);

        // One-time: mark Executed before spawning so a reload on the same day cannot re-fire.
        foreach (var contract in contractsForToday.Where(c => c.Schedule == ContractSchedule.OneTime))
        {
            _store.Update(contract.Id, contract with { Status = ContractStatus.Executed });
            _orchestrator.StartShift(contract);
        }

        // Recurring: fire shift but do NOT mark Executed — the contract remains Active
        // so it fires again tomorrow. Full recurring lifecycle (daily deposit deduction,
        // festival skip, can't-afford mail) ships in U-15.
        foreach (var contract in contractsForToday.Where(c => c.Schedule == ContractSchedule.Recurring))
        {
            _orchestrator.StartShift(contract);
        }
    }

    private static GameDate CurrentGameDate()
    {
        var season = Enum.Parse<Dayswork.Core.Domain.Season>(Game1.currentSeason, ignoreCase: true);
        return new GameDate(Game1.dayOfMonth, season, Game1.year);
    }
}
