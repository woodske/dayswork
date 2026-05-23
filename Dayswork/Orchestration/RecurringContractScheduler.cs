using System.Linq;
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
// the full daily lifecycle: festival skip + courtesy letter, today's rain-aware rate + deposit,
// affordability gate, and deduct-then-start. Single-active-contract invariant (DEV-U15-01) is enforced
// at hire time, so the loop processes at most one contract per day.
internal sealed class RecurringContractScheduler
{
    private readonly IContractStore     _store;
    private readonly ShiftOrchestrator  _orchestrator;
    private readonly CalendarHandlers   _calendar;
    private readonly IRateCalculator    _rateCalc;
    private readonly IDepositCalculator _depositCalc;
    private readonly ModConfigManager   _configManager;
    private readonly IMailDispatcher    _mail;

    public RecurringContractScheduler(
        IContractStore     store,
        ShiftOrchestrator  orchestrator,
        CalendarHandlers   calendar,
        IRateCalculator    rateCalc,
        IDepositCalculator depositCalc,
        ModConfigManager   configManager,
        IMailDispatcher    mail)
    {
        _store         = store;
        _orchestrator  = orchestrator;
        _calendar      = calendar;
        _rateCalc      = rateCalc;
        _depositCalc   = depositCalc;
        _configManager = configManager;
        _mail          = mail;
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
            // Festival gate (DEV-U15-02): the worker never shows; a courtesy letter is sent either way.
            if (festival)
            {
                HandleFestival(contract);
                continue;
            }

            if (contract.Schedule == ContractSchedule.OneTime)
            {
                // One-time: deposit already paid at hire. Mark Executed before spawning so a reload on
                // the same day cannot re-fire.
                _store.Update(contract.Id, contract with { Status = ContractStatus.Executed });
                _orchestrator.StartShift(contract, contract.DepositAmount, contract.HourlyRate, config);
            }
            else
            {
                StartRecurring(contract, config);
            }
        }
    }

    // BR-CAL-03 / BR-DAY-03: on a festival day the recurring contract takes no deposit and stays Active;
    // a one-time contract is consumed (Executed) and its already-paid deposit is refunded by same-day mail.
    private void HandleFestival(Contract contract)
    {
        if (contract.Schedule == ContractSchedule.OneTime)
        {
            _store.Update(contract.Id, contract with { Status = ContractStatus.Executed });
            _mail.QueueFestivalNotice(contract, contract.DepositAmount);
        }
        else
        {
            _mail.QueueFestivalNotice(contract, 0);
        }

        ModEntry.ModMonitor.Log(I18nHelper.Get("log.festival.skipped"), LogLevel.Info);
    }

    // Full per-recurring-day sequence (BR-DAY-04..07, BR-AFF-01..03).
    private void StartRecurring(Contract contract, IConfigSnapshot config)
    {
        // Today's rate excludes the Water Crops surcharge on rainy days (FR-PAY-07 / DEV-U15-05); the
        // task itself stays enabled. Config is the live snapshot at day-start (FR-PAY-08).
        var rate    = _rateCalc.Calculate(contract.EnabledTasks, config, _calendar.IsRainyToday());
        var hours   = DepositHoursPolicy.EstimateBillableHours(contract.Zones, contract.EnabledTasks.Count, config);
        var deposit = _depositCalc.Calculate(hours, rate) is PositiveDeposit p ? p.Amount : 0;

        // Affordability gate (FR-PAY-04 / FD-Q5=A): skip + mail, stay Active, retry tomorrow.
        if (Game1.player.Money < deposit)
        {
            _mail.QueueCannotAffordNotice(contract, deposit - Game1.player.Money);
            ModEntry.ModMonitor.Log(
                $"[Dayswork] Recurring contract {contract.Id.Value} unaffordable today (need {deposit}g, have {Game1.player.Money}g) — skipped; notice mailed.",
                LogLevel.Info);
            return;
        }

        Game1.player.Money -= deposit;
        _orchestrator.StartShift(contract, deposit, rate, config);
    }

    private static GameDate CurrentGameDate()
    {
        var season = Enum.Parse<Dayswork.Core.Domain.Season>(Game1.currentSeason, ignoreCase: true);
        return new GameDate(Game1.dayOfMonth, season, Game1.year);
    }
}
