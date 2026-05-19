namespace Dayswork.Core.Pricing;

using Dayswork.Core.Config;
using Dayswork.Core.Domain;

public interface IRateCalculator
{
    int Calculate(IEnumerable<TaskKind> enabledTasks, IConfigSnapshot config, bool isRaining);
}
