using Dayswork.Core.Config;
using Dayswork.Core.Domain;
using Dayswork.Core.Persistence;
using Dayswork.Core.Pricing;
using Dayswork.Core.Upgrades;
using Dayswork.Guards;
using Dayswork.Integration;
using Dayswork.Net;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace Dayswork.Orchestration;

// The fixed-price day-start lifecycle: recurring terms refresh at 6am, affordability/notice
// decisions from the rebuilt terms snapshot, festival no-charge skips, and same-day HUD notices.
// Every office with a contract due today gets its own concurrent shift via the fleet. The loop
// runs in deterministic hire order so wallet charges and work-claim priority are stable day to
// day, and each contract is evaluated inside its own try/catch: one bad contract must not stop
// the other offices' farmhands from going to work.
internal sealed class RecurringContractScheduler
{
    private readonly OfficeContractStore _store;
    private readonly ShiftFleet _fleet;
    private readonly CalendarHandlers _calendar;
    private readonly RecurringDayStartDecisionEngine _decisionEngine;
    private readonly ModConfigManager _configManager;
    private readonly FarmhandUpgradeStore _upgradeStore;
    private readonly IShiftOutcomeDispatcher _shiftOutcomes;
    private readonly DaysworkSuspension _suspension;

    public RecurringContractScheduler(
        OfficeContractStore store,
        ShiftFleet fleet,
        CalendarHandlers calendar,
        RecurringDayStartDecisionEngine decisionEngine,
        ModConfigManager configManager,
        FarmhandUpgradeStore upgradeStore,
        IShiftOutcomeDispatcher shiftOutcomes,
        DaysworkSuspension suspension)
    {
        _store = store;
        _fleet = fleet;
        _calendar = calendar;
        _decisionEngine = decisionEngine;
        _configManager = configManager;
        _upgradeStore = upgradeStore;
        _shiftOutcomes = shiftOutcomes;
        _suspension = suspension;
    }

    public void OnDayStarted(object? sender, DayStartedEventArgs e)
    {
        // The shift engine is the host's alone: a client never starts a shift, spends money, or
        // writes a contract (2.0 plan D2).
        if (!Authority.IsHost)
            return;

        // While an incompatible peer is connected, no worker goes out — the NPC is a custom type
        // their game cannot read (plan D8). The suspension lifts on a later day start.
        if (_suspension.IsSuspended)
        {
            ModEntry.ModMonitor.Log(
                "[Dayswork] Skipped day start: Dayswork is suspended while an incompatible player is connected.",
                DevLog.WarnLevel);
            return;
        }

        var today = CurrentGameDate();
        // Deterministic order (the store is dictionary-backed): earliest hire first, contract id as
        // the tiebreak. This fixes both wallet-charge order and, through it, which contract wins a
        // work claim where two offices' scopes overlap.
        var contractsForToday = _store.ScheduledForDate(today.Day, today.Season, today.Year)
            .OrderBy(c => c.HireDate.Year)
            .ThenBy(c => c.HireDate.Season)
            .ThenBy(c => c.HireDate.Day)
            .ThenBy(c => c.Id.Value)
            .ToList();
        if (contractsForToday.Count == 0)
            return;

        // WorkOnHolidays is a global setting, so the festival gate does not depend on whose
        // upgrades apply; each contract's own config is resolved per owner below.
        var holidaySkip = _calendar.IsFestivalToday() && !_configManager.CurrentSnapshot.WorkOnHolidays;

        foreach (var contract in contractsForToday)
        {
            try
            {
                // Upgrades belong to the office's OWNER (2.0 plan D9), so the config a shift runs
                // under is resolved per contract rather than once for the farm.
                var config = FarmhandUpgradeEffects.Apply(
                    _configManager.CurrentSnapshot,
                    _upgradeStore.For(contract.OwnerId));
                StartOne(contract, config, holidaySkip);
            }
            catch (Exception ex)
            {
                ModEntry.ModMonitor.Log(
                    $"[Dayswork] Day-start evaluation failed for contract {contract.Id.Value}: {ex.Message}",
                    DevLog.WarnLevel);
            }
        }
    }

    private void StartOne(Contract contract, ConfigSnapshot config, bool holidaySkip)
    {
        // An office whose owner no longer exists (their farmhand slot was deleted) cannot be
        // charged or paid, so it is skipped until the host claims it (2.0 plan D4). Nothing is
        // charged and the contract is left alone — claiming is a deliberate act.
        if (Sponsor.Resolve(contract.OwnerId) is null)
        {
            ModEntry.ModMonitor.Log(
                $"[Dayswork] Contract {contract.Id.Value}'s owner ({contract.OwnerId}) is not a player on this save — skipping it. The host can claim the office to take it over.",
                DevLog.WarnLevel);
            return;
        }

        // An office demolished while its contract was still active: the contract goes with the
        // building (no refund — see the 2.0 plan's D1). Nothing is charged for it today.
        if (OfficeResolver.TryGet(contract.OfficeId) is null)
        {
            _store.RemoveOffice(contract.OfficeId);
            _shiftOutcomes.ShowContractLostNotice(contract.OwnerId);
            ModEntry.ModMonitor.Log(
                $"[Dayswork] Contract {contract.Id.Value}'s office no longer exists — the contract has been dropped with no charge.",
                DevLog.WarnLevel);
            return;
        }

        // Festival gate for one-time contracts: the contract is consumed and the already-paid
        // fixed price is returned by direct refund plus a same-day HUD notice.
        if (holidaySkip && contract.Schedule == ContractSchedule.OneTime)
        {
            HandleFestival(contract);
            return;
        }

        if (contract.Schedule == ContractSchedule.OneTime)
        {
            // One-time: fixed price already paid at hire. Mark Executed before spawning so a
            // reload on the same day cannot re-fire.
            _store.Update(contract.OfficeId, contract with { Status = ContractStatus.Executed });
            _fleet.StartShift(contract, config);
        }
        else
        {
            StartRecurring(contract, config, holidaySkip);
        }
    }

    // Festival day handling: recurring contracts stay Active with no charge, while a one-time contract
    // is consumed (Executed) and its already-paid contract price is refunded directly.
    private void HandleFestival(Contract contract)
    {
        if (contract.Schedule == ContractSchedule.OneTime)
        {
            _store.Update(contract.OfficeId, contract with { Status = ContractStatus.Executed });
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
        // Affordability and the charge are both the contract owner's — their wallet, which under
        // co-op's shared-money setting (and in single-player) is the one everybody draws on.
        var outcome = _decisionEngine.Evaluate(contract, config, festivalToday, Sponsor.Money(contract.OwnerId));
        if (outcome.ShouldPersistTermsSnapshot && outcome.Refresh.TermsSnapshot is not null)
            _store.ReplaceTermsSnapshot(contract.OfficeId, outcome.Refresh.TermsSnapshot);

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
            Sponsor.Charge(contract.OwnerId, outcome.DailyPrice);

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
