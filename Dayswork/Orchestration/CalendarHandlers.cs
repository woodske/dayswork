using System;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace Dayswork.Orchestration;

// The single place that reads Stardew festival/weather state, plus
// the at-save hook that stops and settles every live worker when the farmer sleeps. Each predicate
// fails safe (returns the non-festival / non-rain default and logs) rather than throwing.
internal sealed class CalendarHandlers
{
    private readonly ShiftFleet _fleet;

    public CalendarHandlers(ShiftFleet fleet) => _fleet = fleet;

    // True on a festival calendar day (the worker skips and a courtesy letter is sent).
    public bool IsFestivalToday()
    {
        try
        {
            return Utility.isFestivalDay(Game1.dayOfMonth, Game1.season);
        }
        catch (Exception ex)
        {
            ModEntry.ModMonitor.Log($"[Dayswork] Festival check failed; treating today as non-festival: {ex.Message}", LogLevel.Trace);
            return false;
        }
    }

    // True when today's farm weather is rain/storm. Feeds the rate calculation only (the
    // Water Crops task is not removed).
    public bool IsRainyToday()
    {
        try
        {
            return Game1.IsRainingHere(Game1.getFarm());
        }
        catch (Exception ex)
        {
            ModEntry.ModMonitor.Log($"[Dayswork] Weather check failed; treating today as dry: {ex.Message}", LogLevel.Trace);
            return false;
        }
    }

    // GameLoop.Saving handler (registered before ContractPersistenceAdapter.OnSaving so every live
    // shift settles into today's state before contracts persist and the day rolls over).
    public void OnSavingHook(object? sender, SavingEventArgs e) => _fleet.StopForSleepAndSettle();
}
