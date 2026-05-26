using Dayswork.Core.Config;
using Dayswork.Core.Domain;
using Dayswork.Core.Persistence;
using Dayswork.Core.Pricing;
using Dayswork.Guards;
using Dayswork.Integration;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace Dayswork.Orchestration;

// M-13 RecurringContractScheduler (Pattern R / Service S-D). Promoted from the U-10 one-time stub to
// the rebuilt fixed-price day-start lifecycle: recurring terms refresh at 6am, affordability/notice
// decisions from the rebuilt terms snapshot, festival no-charge skips, and same-day no-worker mail.
// Single-active-contract invariant (DEV-U15-01) is enforced at hire time, so the loop processes at
// most one contract per day.
internal sealed class RecurringContractScheduler
{
    private readonly IContractStore _store;
    private readonly ShiftOrchestrator _orchestrator;
    private readonly CalendarHandlers _calendar;
    private readonly RecurringDayStartDecisionEngine _decisionEngine;
    private readonly ModConfigManager _configManager;
    private readonly IMailDispatcher _mail;

    public RecurringContractScheduler(
        IContractStore store,
        ShiftOrchestrator orchestrator,
        CalendarHandlers calendar,
        RecurringDayStartDecisionEngine decisionEngine,
        ModConfigManager configManager,
        IMailDispatcher mail)
    {
        _store = store;
        _orchestrator = orchestrator;
        _calendar = calendar;
        _decisionEngine = decisionEngine;
        _configManager = configManager;
        _mail = mail;
    }

    public void OnDayStarted(object? sender, DayStartedEventArgs e)
    {
        // REL-U10-01: multiplayer guard — no-op in multiplayer sessions.
        if (MultiplayerGuard.IsMultiplayer())
            return;

        var today = CurrentGameDate();
        var contractsForToday = _store.ListActiveForDate(today.Day, today.Season, today.Year);
        var festival = _calendar.IsFestivalToday();
        var config = _configManager.CurrentSnapshot;

        foreach (var contract in contractsForToday)
        {
            try
            {
                // Festival gate for one-time contracts: the contract is consumed and the already-paid
                // fixed price is returned by same-day mail.
                if (festival && contract.Schedule == ContractSchedule.OneTime)
                {
                    HandleFestival(contract);
                    continue;
                }

                if (contract.Schedule == ContractSchedule.OneTime)
                {
                    // One-time: fixed price already paid at hire. Mark Executed before spawning so a
                    // reload on the same day cannot re-fire.
                    _store.Update(contract.Id, contract with { Status = ContractStatus.Executed });
                    _orchestrator.StartShift(contract, config);
                }
                else
                {
                    StartRecurring(contract, config, festival);
                }
            }
            catch (Exception ex)
            {
                ModEntry.ModMonitor.Log(
                    $"[Dayswork] Recurring day-start evaluation failed for contract {contract.Id.Value}: {ex.Message}",
                    LogLevel.Warn);
            }
        }
    }

    // Festival day handling: recurring contracts stay Active with no charge, while a one-time contract
    // is consumed (Executed) and its already-paid contract price is refunded by same-day mail.
    private void HandleFestival(Contract contract)
    {
        if (contract.Schedule == ContractSchedule.OneTime)
        {
            _store.Update(contract.Id, contract with { Status = ContractStatus.Executed });
            _mail.QueueFestivalNotice(contract, FestivalRefundAmount(contract));
        }
        else
        {
            _mail.QueueFestivalNotice(contract, 0);
        }

        ModEntry.ModMonitor.Log(I18nHelper.Get("log.festival.skipped"), LogLevel.Info);
    }

    // Full per-recurring-day sequence: rebuild first, persist refreshed terms when valid, then select
    // the festival / needs-attention / cannot-afford / start-shift path from the same rebuilt terms.
    private void StartRecurring(Contract contract, IConfigSnapshot config, bool festivalToday)
    {
        var outcome = _decisionEngine.Evaluate(contract, config, festivalToday, Game1.player.Money);
        if (outcome.ShouldPersistTermsSnapshot && outcome.Refresh.TermsSnapshot is not null)
            _store.ReplaceTermsSnapshot(contract.Id, outcome.Refresh.TermsSnapshot);

        switch (outcome.NoticeKind)
        {
            case RecurringDayStartNoticeKind.NeedsAttention:
                _mail.QueueNeedsAttentionNotice(contract);
                ModEntry.ModMonitor.Log(
                    $"[Dayswork] Recurring contract {contract.Id.Value} needs attention before it can be rebuilt for today — skipped with no charge.",
                    LogLevel.Info);
                return;
            case RecurringDayStartNoticeKind.FestivalSkip:
                _mail.QueueFestivalNotice(contract, 0);
                ModEntry.ModMonitor.Log(
                    $"[Dayswork] Recurring contract {contract.Id.Value} refreshed for a festival day — no charge taken and no worker spawned.",
                    LogLevel.Info);
                return;
            case RecurringDayStartNoticeKind.CannotAfford:
                _mail.QueueCannotAffordNotice(contract, outcome.DailyPrice, outcome.Shortfall);
                ModEntry.ModMonitor.Log(
                    $"[Dayswork] Recurring contract {contract.Id.Value} is unaffordable today (price {outcome.DailyPrice}g, short by {outcome.Shortfall}g) — skipped with refreshed terms preserved.",
                    LogLevel.Info);
                return;
        }

        if (outcome.ShouldChargePlayer)
            Game1.player.Money -= outcome.DailyPrice;

        if (outcome.ShouldStartShift)
        {
            var refreshedContract = outcome.Refresh.TermsSnapshot is null
                ? contract
                : contract with { TermsSnapshot = outcome.Refresh.TermsSnapshot };
            _orchestrator.StartShift(refreshedContract, config);
        }
    }

    private static int FestivalRefundAmount(Contract contract)
    {
        if (contract.TermsSnapshot is not null)
            return contract.TermsSnapshot.Pricing.TotalPrice;

        return contract.DepositAmount;
    }

    private static GameDate CurrentGameDate()
    {
        var season = Enum.Parse<Dayswork.Core.Domain.Season>(Game1.currentSeason, ignoreCase: true);
        return new GameDate(Game1.dayOfMonth, season, Game1.year);
    }
}
