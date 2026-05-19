namespace Dayswork.Core.Pricing;

using Dayswork.Core.Config;
using Dayswork.Core.Domain;

public sealed class RateCalculator : IRateCalculator
{
    public int Calculate(IEnumerable<TaskKind> enabledTasks, IConfigSnapshot config, bool isRaining)
    {
        var rate = config.BaseRate;

        foreach (var task in enabledTasks)
        {
            if (isRaining && task == TaskKind.WaterCrops)
                continue;

            rate += config.TaskIncrements[task];
        }

        return rate;
    }
}
