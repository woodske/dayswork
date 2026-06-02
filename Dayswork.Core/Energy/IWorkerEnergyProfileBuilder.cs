namespace Dayswork.Core.Energy;

using Dayswork.Core.Config;
using Dayswork.Core.Domain;

public interface IWorkerEnergyProfileBuilder
{
    /// <summary>
    /// Builds the worker energy profile for a contract: daily capacity comes from the purchased
    /// <paramref name="tier"/>, action costs come from config.
    /// </summary>
    WorkerEnergyProfile BuildProfile(IReadOnlySet<TaskKind> enabledTasks, EnergyTier tier, IConfigSnapshot config);
}
