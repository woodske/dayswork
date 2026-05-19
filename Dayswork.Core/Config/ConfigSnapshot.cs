namespace Dayswork.Core.Config;

using Dayswork.Core.Domain;

public sealed record ConfigSnapshot(
    int BaseRate,
    IReadOnlyDictionary<TaskKind, int> TaskIncrements,
    double AverageSpeedConstant,
    int HardCapTime,
    int StuckInitialWaitMinutes,
    int StuckPostTeleportWaitMinutes
) : IConfigSnapshot
{
    // IReadOnlyDictionary uses reference equality, so override to get structural equality.
    public bool Equals(ConfigSnapshot? other) =>
        other is not null
        && BaseRate == other.BaseRate
        && AverageSpeedConstant == other.AverageSpeedConstant
        && HardCapTime == other.HardCapTime
        && StuckInitialWaitMinutes == other.StuckInitialWaitMinutes
        && StuckPostTeleportWaitMinutes == other.StuckPostTeleportWaitMinutes
        && TaskIncrements.Count == other.TaskIncrements.Count
        && TaskIncrements.All(kvp =>
            other.TaskIncrements.TryGetValue(kvp.Key, out var v) && v == kvp.Value);

    public override int GetHashCode() =>
        HashCode.Combine(BaseRate, AverageSpeedConstant, HardCapTime,
            StuckInitialWaitMinutes, StuckPostTeleportWaitMinutes, TaskIncrements.Count);
}
