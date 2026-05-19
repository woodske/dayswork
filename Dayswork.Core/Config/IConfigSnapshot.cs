namespace Dayswork.Core.Config;

using Dayswork.Core.Domain;

public interface IConfigSnapshot
{
    int BaseRate { get; }
    IReadOnlyDictionary<TaskKind, int> TaskIncrements { get; }
    double AverageSpeedConstant { get; }
    int HardCapTime { get; }
    int StuckInitialWaitMinutes { get; }
    int StuckPostTeleportWaitMinutes { get; }
}
