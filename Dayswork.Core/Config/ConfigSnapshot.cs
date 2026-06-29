namespace Dayswork.Core.Config;

using Dayswork.Core.Domain;
using Dayswork.Core.Energy;

public sealed record ConfigSnapshot(
    int HardCapTime,
    int StuckInitialWaitMinutes,
    int StuckPostTeleportWaitMinutes,
    float WorkerWalkPixelsPerTick,
    int WorkerActionAnimationMs,
    int WorkerEntranceHoldTicks,
    bool WorkOnHolidays,
    bool EagerChestDeposits,
    IReadOnlyDictionary<EnergyTier, int> EnergyTierEnergy,
    IReadOnlyDictionary<EnergyTier, int> EnergyTierPrice,
    IReadOnlyDictionary<WorkActionKind, int> WorkActionCosts
)
{
    // IReadOnlyDictionary uses reference equality, so override to get structural equality.
    public bool Equals(ConfigSnapshot? other) =>
        other is not null
        && HardCapTime == other.HardCapTime
        && StuckInitialWaitMinutes == other.StuckInitialWaitMinutes
        && StuckPostTeleportWaitMinutes == other.StuckPostTeleportWaitMinutes
        && WorkerWalkPixelsPerTick.Equals(other.WorkerWalkPixelsPerTick)
        && WorkerActionAnimationMs == other.WorkerActionAnimationMs
        && WorkerEntranceHoldTicks == other.WorkerEntranceHoldTicks
        && WorkOnHolidays == other.WorkOnHolidays
        && EagerChestDeposits == other.EagerChestDeposits
        && DictionaryEquals(EnergyTierEnergy, other.EnergyTierEnergy)
        && DictionaryEquals(EnergyTierPrice, other.EnergyTierPrice)
        && DictionaryEquals(WorkActionCosts, other.WorkActionCosts);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(HardCapTime);
        hash.Add(StuckInitialWaitMinutes);
        hash.Add(StuckPostTeleportWaitMinutes);
        hash.Add(WorkerWalkPixelsPerTick);
        hash.Add(WorkerActionAnimationMs);
        hash.Add(WorkerEntranceHoldTicks);
        hash.Add(WorkOnHolidays);
        hash.Add(EagerChestDeposits);
        hash.Add(EnergyTierEnergy.Count);
        hash.Add(EnergyTierPrice.Count);
        hash.Add(WorkActionCosts.Count);
        return hash.ToHashCode();
    }

    private static bool DictionaryEquals<TKey>(
        IReadOnlyDictionary<TKey, int> left,
        IReadOnlyDictionary<TKey, int> right)
        where TKey : notnull
    {
        if (left.Count != right.Count)
            return false;

        return left.All(kvp => right.TryGetValue(kvp.Key, out var value) && value == kvp.Value);
    }
}
