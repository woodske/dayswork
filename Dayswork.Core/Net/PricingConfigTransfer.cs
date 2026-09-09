using Dayswork.Core.Config;
using Dayswork.Core.Domain;
using Dayswork.Core.Energy;

namespace Dayswork.Core.Net;

/// <summary>
/// Moves the host's pricing and energy numbers between its <see cref="ConfigSnapshot"/> and the
/// wire (R9). A client's own config file may say something entirely different from the host's, and
/// the host's is what gets charged — so the client prices its draft against these rather than its
/// own, and a preview that agrees with the commit is the point.
/// </summary>
public static class PricingConfigTransfer
{
    public static SnapshotPricingConfig From(ConfigSnapshot config) =>
        new()
        {
            EnergyTierEnergy = new Dictionary<EnergyTier, int>(config.EnergyTierEnergy),
            EnergyTierPrice = new Dictionary<EnergyTier, int>(config.EnergyTierPrice),
            WorkActionCosts = new Dictionary<WorkActionKind, int>(config.WorkActionCosts),
        };

    /// <summary>
    /// The local snapshot with the host's pricing and energy tables laid over it. Entries are
    /// merged key by key rather than replaced wholesale: a peer that sends a partial table (an
    /// older build, a truncated message) then costs the missing entries their local value instead
    /// of a thrown snapshot, and <see cref="ConfigValueResolver"/> still backstops anything absurd.
    /// </summary>
    public static ConfigSnapshot ApplyTo(ConfigSnapshot local, SnapshotPricingConfig? host)
    {
        if (host is null)
            return local;

        return ConfigSnapshotFactory.Create(
            local.HardCapTime,
            local.StuckInitialWaitMinutes,
            local.StuckPostTeleportWaitMinutes,
            local.WorkerWalkPixelsPerTick,
            local.WorkerActionAnimationMs,
            local.WorkerEntranceHoldTicks,
            local.WorkOnHolidays,
            local.EagerChestDeposits,
            Merge(local.EnergyTierEnergy, host.EnergyTierEnergy),
            Merge(local.EnergyTierPrice, host.EnergyTierPrice),
            Merge(local.WorkActionCosts, host.WorkActionCosts));
    }

    private static Dictionary<TKey, int> Merge<TKey>(
        IReadOnlyDictionary<TKey, int> local,
        Dictionary<TKey, int> incoming)
        where TKey : notnull
    {
        var merged = new Dictionary<TKey, int>();
        foreach (var (key, value) in local)
            merged[key] = value;
        foreach (var (key, value) in incoming)
            merged[key] = value;

        return merged;
    }
}
