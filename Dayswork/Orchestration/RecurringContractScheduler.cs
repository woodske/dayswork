using Dayswork.Core.Config;
using Dayswork.Core.Domain;
using Dayswork.Core.Persistence;
using Dayswork.Core.Pricing;
using Dayswork.Core.Upgrades;
using Dayswork.Guards;
using Dayswork.Integration;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace Dayswork.Orchestration;

// The fixed-price day-start lifecycle: recurring terms refresh at 6am, affordability/notice
// decisions from the rebuilt terms snapshot, festival no-charge skips, and same-day HUD notices.
// Each contract scheduled for today gets its own concurrent shift via the fleet; the loop runs
// in deterministic hire order so wallet charges and work-claim priority are stable day to day.
internal sealed class RecurringContractScheduler
{
    private readonly ContractStore _store;
    private readonly ShiftFleet _fleet;
    private readonly CalendarHandlers _calendar;
    private readonly RecurringDayStartDecisionEngine _decisionEngine;
    private readonly ModConfigManager _configManager;
    private readonly FarmhandUpgradeStore _upgradeStore;
    private readonly IShiftOutcomeDispatcher _shiftOutcomes;

    public RecurringContractScheduler(
        ContractStore store,
        ShiftFleet fleet,
        CalendarHandlers calendar,
        RecurringDayStartDecisionEngine decisionEngine,
        ModConfigManager configManager,
        FarmhandUpgradeStore upgradeStore,
        IShiftOutcomeDispatcher shiftOutcomes)
    {
        _store = store;
        _fleet = fleet;
        _calendar = calendar;
        _decisionEngine = decisionEngine;
        _configManager = configManager;
        _upgradeStore = upgradeStore;
        _shiftOutcomes = shiftOutcomes;
    }

    public void OnDayStarted(object? sender, DayStartedEventArgs e)
    {
        // Multiplayer guard — no-op in multiplayer sessions.
        if (MultiplayerGuard.IsMultiplayer())
            return;

        var today = CurrentGameDate();
        // Deterministic order (store is dictionary-backed): earliest hire first, id as tiebreak —
        // fixes both wallet-charge order and work-claim priority for overlapping scopes.
        var contractsForToday = _store.ListActiveForDate(today.Day, today.Season, today.Year)
            .OrderBy(c => c.HireDate.Year)
            .ThenBy(c => c.HireDate.Season)
            .ThenBy(c => c.HireDate.Day)
            .ThenBy(c => c.Id.Value)
            .ToList();
        var config = FarmhandUpgradeEffects.Apply(_configManager.CurrentSnapshot, _upgradeStore.State);
        var holidaySkip = _calendar.IsFestivalToday() && !config.WorkOnHolidays;

        foreach (var contract in contractsForToday)
        {
            try
            {
                // Festival gate for one-time contracts: the contract is consumed and the already-paid
                // fixed price is returned by direct refund plus a same-day HUD notice.
                if (holidaySkip && contract.Schedule == ContractSchedule.OneTime)
                {
                    HandleFestival(contract);
                    continue;
                }

                if (contract.Schedule == ContractSchedule.OneTime)
                {
                    // One-time: fixed price already paid at hire. Mark Executed before spawning so a
                    // reload on the same day cannot re-fire.
                    _store.Update(contract.Id, contract with { Status = ContractStatus.Executed });
                    _fleet.StartShift(contract, config);
                }
                else
                {
                    StartRecurring(contract, config, holidaySkip);
                }
            }
            catch (Exception ex)
            {
                ModEntry.ModMonitor.Log(
                    $"[Dayswork] Recurring day-start evaluation failed for contract {contract.Id.Value}: {ex.Message}",
                    DevLog.WarnLevel);
            }
        }
    }

    // Festival day handling: recurring contracts stay Active with no charge, while a one-time contract
    // is consumed (Executed) and its already-paid contract price is refunded directly.
    private void HandleFestival(Contract contract)
    {
        if (contract.Schedule == ContractSchedule.OneTime)
        {
            _store.Update(contract.Id, contract with { Status = ContractStatus.Executed });
            _shiftOutcomes.ShowFestivalNotice(contract, contract.TermsSnapshot.Pricing.TotalPrice);
        }
        else
        {
            _shiftOutcomes.ShowFestivalNotice(contract, 0);
        }

        ModEntry.ModMonitor.Log(I18nHelper.Get("log.festival.skipped"), LogLevel.Trace);
    }

    // Full per-recurring-day sequence: rebuild first, persist refreshed terms when valid, then select
    // the festival / needs-attention / cannot-afford / start-shift path from the same rebuilt terms.
    private void StartRecurring(Contract contract, ConfigSnapshot config, bool festivalToday)
    {
        var outcome = _decisionEngine.Evaluate(contract, config, festivalToday, Game1.player.Money);
        if (outcome.ShouldPersistTermsSnapshot && outcome.Refresh.TermsSnapshot is not null)
            _store.ReplaceTermsSnapshot(contract.Id, outcome.Refresh.TermsSnapshot);

        switch (outcome.NoticeKind)
        {
            case RecurringDayStartNoticeKind.NeedsAttention:
                _shiftOutcomes.ShowNeedsAttentionNotice(contract);
                ModEntry.ModMonitor.Log(
                    $"[Dayswork] Recurring contract {contract.Id.Value} needs attention before it can be rebuilt for today — skipped with no charge.",
                    LogLevel.Trace);
                return;
            case RecurringDayStartNoticeKind.FestivalSkip:
                _shiftOutcomes.ShowFestivalNotice(contract, 0);
                ModEntry.ModMonitor.Log(
                    $"[Dayswork] Recurring contract {contract.Id.Value} refreshed for a festival day — no charge taken and no worker spawned.",
                    LogLevel.Trace);
                return;
            case RecurringDayStartNoticeKind.CannotAfford:
                _shiftOutcomes.ShowCannotAffordNotice(contract, outcome.DailyPrice, outcome.Shortfall);
                ModEntry.ModMonitor.Log(
                    $"[Dayswork] Recurring contract {contract.Id.Value} is unaffordable today (price {outcome.DailyPrice}g, short by {outcome.Shortfall}g) — skipped with refreshed terms preserved.",
                    LogLevel.Trace);
                return;
        }

        if (outcome.ShouldChargePlayer)
            Game1.player.Money -= outcome.DailyPrice;

        if (outcome.ShouldStartShift)
        {
            var refreshedContract = outcome.Refresh.TermsSnapshot is null
                ? contract
                : contract with { TermsSnapshot = outcome.Refresh.TermsSnapshot };
            _fleet.StartShift(refreshedContract, config);
        }
    }

    private static GameDate CurrentGameDate()
    {
        var season = Enum.Parse<Dayswork.Core.Domain.Season>(Game1.currentSeason, ignoreCase: true);
        return new GameDate(Game1.dayOfMonth, season, Game1.year);
    }
}
