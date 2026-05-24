namespace Dayswork.Core.Energy;

using Dayswork.Core.Config;
using Dayswork.Core.Domain;

public interface IWorkerEnergyProfileBuilder
{
    WorkerEnergyProfile BuildProfile(IReadOnlySet<TaskKind> enabledTasks, IConfigSnapshot config);
}
