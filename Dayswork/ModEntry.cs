using Dayswork.Core.Config;
using Dayswork.Core.Persistence;
using Dayswork.Core.Pricing;
using Dayswork.Integration;
using Dayswork.UI;
using HarmonyLib;
using StardewModdingAPI;

namespace Dayswork;

public sealed class ModEntry : Mod
{
    // Exposed as internal static so static patch classes can emit log messages and
    // invoke coordinator methods without constructor injection.
    internal static IMonitor ModMonitor { get; private set; } = null!;
    internal static HiringFlowCoordinator Coordinator { get; private set; } = null!;

    public override void Entry(IModHelper helper)
    {
        ModMonitor = this.Monitor;
        I18nHelper.Init(helper);

        // ── Core singletons (dependency order) ──────────────────────────────
        var logWarning  = (string msg) => this.Monitor.Log(msg, LogLevel.Warn);
        var config      = ConfigDefaults.Build();
        var rateCalc    = new RateCalculator();
        var depositCalc = new DepositCalculator();
        var hoursEst    = new HoursEstimator();
        var store       = new ContractStore(logWarning);
        var serializer  = new SaveDataSerializer(logWarning);

        // ── Mod singletons ───────────────────────────────────────────────────
        Coordinator = new HiringFlowCoordinator(rateCalc, depositCalc, hoursEst, config, store);
        var persistAdapter = new ContractPersistenceAdapter(
            store, serializer, helper.Data, this.ModManifest.Version.ToString());

        // ── Event registrations ──────────────────────────────────────────────
        helper.Events.GameLoop.SaveLoaded += persistAdapter.OnSaveLoaded;
        helper.Events.GameLoop.Saving     += persistAdapter.OnSaving;

        // ── Harmony patches ──────────────────────────────────────────────────
        new Harmony(this.ModManifest.UniqueID).PatchAll();

        // TODO: REMOVE before release — debug command for verifying save/load persistence (task #1 play-test)
        RegisterDebugCommands(helper, store);

        this.Monitor.Log("Dayswork loaded", LogLevel.Info);
    }

    // TODO: REMOVE before release — see RegisterDebugCommands call above
    private void RegisterDebugCommands(IModHelper helper, ContractStore store)
    {
        helper.ConsoleCommands.Add(
            "dayswork_list",
            "Lists all contracts currently in memory. Used to verify save/load persistence.",
            (_, _) =>
            {
                var contracts = store.List();
                if (contracts.Count == 0)
                {
                    this.Monitor.Log("No contracts in store.", LogLevel.Info);
                    return;
                }
                foreach (var c in contracts)
                {
                    this.Monitor.Log(
                        $"[{c.Id.Value}] status={c.Status} tasks={string.Join(",", c.EnabledTasks)} " +
                        $"hired={c.HireDate.Day} {c.HireDate.Season} Y{c.HireDate.Year} " +
                        $"deposit={c.DepositAmount}g rate={c.HourlyRate}g/hr",
                        LogLevel.Info);
                }
            });
    }
}
