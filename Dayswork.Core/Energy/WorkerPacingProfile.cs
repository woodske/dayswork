namespace Dayswork.Core.Energy;

using Dayswork.Core.Config;

public sealed record WorkerPacingProfile(
    float WalkPixelsPerTick,
    double ActionAnimationMs,
    int EntranceHoldTicks)
{
    public static WorkerPacingProfile FromConfig(IConfigSnapshot config) =>
        new(
            WalkPixelsPerTick: config.WorkerWalkPixelsPerTick,
            ActionAnimationMs: config.WorkerActionAnimationMs,
            EntranceHoldTicks: config.WorkerEntranceHoldTicks);
}
