using Dayswork.Integration;
using HarmonyLib;
using StardewModdingAPI;

namespace Dayswork;

public sealed class ModEntry : Mod
{
    // Exposed as internal static so Harmony patch classes (which are static and
    // cannot use constructor injection) can emit log messages.
    // Named ModMonitor rather than Monitor to avoid shadowing Mod.Monitor.
    internal static IMonitor ModMonitor { get; private set; } = null!;

    public override void Entry(IModHelper helper)
    {
        ModMonitor = this.Monitor;
        I18nHelper.Init(helper);

        var harmony = new Harmony(this.ModManifest.UniqueID);
        harmony.PatchAll();

        this.Monitor.Log("Dayswork loaded", LogLevel.Info);
    }
}
