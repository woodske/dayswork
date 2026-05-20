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

        // Stub: one-time contracts only. Recurring lifecycle deferred to U-15.
        var toFire = _store
            .ListActiveForDate(today.Day, today.Season, today.Year)
            .Where(c => c.Schedule == ContractSchedule.OneTime)
            .ToList();

        foreach (var contract in toFire)
        {
            // Write-before-spawn deduplication guard (SAFE-U10-03).
            // Status is set to Executed BEFORE spawning so a reload on the same day
            // will not re-fire the contract.
            _store.Update(contract.Id, contract with { Status = ContractStatus.Executed });
            _orchestrator.StartShift(contract);
        }
    }

    private static GameDate CurrentGameDate()
    {
        var season = Enum.Parse<Dayswork.Core.Domain.Season>(Game1.currentSeason, ignoreCase: true);
        return new GameDate(Game1.dayOfMonth, season, Game1.year);
    }
}
