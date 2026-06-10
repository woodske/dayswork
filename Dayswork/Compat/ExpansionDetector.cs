using Dayswork.Core.Compat;
using StardewModdingAPI;

namespace Dayswork.Compat;

/// <summary>
/// Discovers which expansion is installed via SMAPI and resolves the active profile.
/// Guarded: any failure logs a warning and falls back to the Vanilla profile, so
/// compatibility detection can never crash or disable the mod.
/// </summary>
internal sealed class ExpansionDetector
{
    private static readonly string[] KnownExpansionModIds =
    {
        SveExpansionProfile.ContentModId,
        SveExpansionProfile.CodeModId,
        SveExpansionProfile.ImmersiveFarm2ModId,
        SveExpansionProfile.GrandpasFarmModId,
        SveExpansionProfile.FrontierFarmModId,
    };

    private readonly IModRegistry _modRegistry;
    private readonly ExpansionProfileSelector _selector;
    private readonly IExpansionProfile _vanillaFallback;
    private readonly IMonitor _monitor;

    public ExpansionDetector(
        IModRegistry modRegistry,
        ExpansionProfileSelector selector,
        IExpansionProfile vanillaFallback,
        IMonitor monitor)
    {
        _modRegistry = modRegistry;
        _selector = selector;
        _vanillaFallback = vanillaFallback;
        _monitor = monitor;
    }

    /// <summary>Resolves and returns the active profile, logging it once. Never throws.</summary>
    public IExpansionProfile ResolveActiveProfile()
    {
        try
        {
            var installed = new HashSet<string>();
            foreach (var id in KnownExpansionModIds)
            {
                if (_modRegistry.IsLoaded(id))
                    installed.Add(id);
            }

            var profile = _selector.Select(installed);
            _monitor.Log($"[Dayswork] Active expansion compatibility profile: {profile.Id}.", LogLevel.Trace);
            return profile;
        }
        catch (Exception ex)
        {
            _monitor.Log($"[Dayswork] Expansion detection failed; falling back to vanilla. {ex.Message}", LogLevel.Warn);
            return _vanillaFallback;
        }
    }
}
