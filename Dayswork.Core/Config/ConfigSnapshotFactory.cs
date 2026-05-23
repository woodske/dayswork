namespace Dayswork.Core.Config;

using System.Collections.ObjectModel;
using Dayswork.Core.Domain;

public static class ConfigSnapshotFactory
{
    public static ConfigSnapshot Create(
        int baseRate,
        IReadOnlyDictionary<TaskKind, int> taskIncrements,
        double averageSpeedConstant,
        int hardCapTime,
        int stuckInitialWaitMinutes,
        int stuckPostTeleportWaitMinutes)
    {
        if (baseRate < 0)
            throw new ArgumentOutOfRangeException(nameof(baseRate), "BaseRate must be non-negative.");

        if (averageSpeedConstant <= 0)
            throw new ArgumentOutOfRangeException(nameof(averageSpeedConstant), "AverageSpeedConstant must be greater than zero.");

        if (hardCapTime < 1000 || hardCapTime > 2600)
            throw new ArgumentOutOfRangeException(nameof(hardCapTime), "HardCapTime must be in Stardew HHMM range [1000, 2600].");

        if (stuckInitialWaitMinutes < 1)
            throw new ArgumentOutOfRangeException(nameof(stuckInitialWaitMinutes), "StuckInitialWaitMinutes must be at least 1.");

        if (stuckPostTeleportWaitMinutes < 1)
            throw new ArgumentOutOfRangeException(nameof(stuckPostTeleportWaitMinutes), "StuckPostTeleportWaitMinutes must be at least 1.");

        var normalizedIncrements = new Dictionary<TaskKind, int>();
        foreach (TaskKind kind in Enum.GetValues<TaskKind>())
        {
            if (!taskIncrements.TryGetValue(kind, out var value))
                throw new InvalidOperationException($"Config snapshot is missing a TaskIncrement entry for {kind}.");

            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(taskIncrements), $"Task increment for {kind} must be non-negative.");

            normalizedIncrements[kind] = value;
        }

        return new ConfigSnapshot(
            baseRate,
            new ReadOnlyDictionary<TaskKind, int>(normalizedIncrements),
            averageSpeedConstant,
            hardCapTime,
            stuckInitialWaitMinutes,
            stuckPostTeleportWaitMinutes);
    }
}
