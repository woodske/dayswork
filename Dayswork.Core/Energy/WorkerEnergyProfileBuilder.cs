namespace Dayswork.Core.Energy;

using System.Collections.ObjectModel;
using Dayswork.Core.Config;
using Dayswork.Core.Domain;

public sealed class WorkerEnergyProfileBuilder : IWorkerEnergyProfileBuilder
{
    private readonly ConfigValueResolver _resolver;

    public WorkerEnergyProfileBuilder(ConfigValueResolver resolver)
    {
        _resolver = resolver;
    }

    public WorkerEnergyProfile BuildProfile(IReadOnlySet<TaskKind> enabledTasks, IConfigSnapshot config)
    {
        var actionCosts = new Dictionary<WorkActionKind, int>();
        foreach (var action in Enum.GetValues<WorkActionKind>())
            actionCosts[action] = _resolver.ResolveWorkActionCost(config, action).Value;

        return new WorkerEnergyProfile(
            DailyCapacity: _resolver.ResolveWorkerDailyEnergyCapacity(config).Value,
            ActionCosts: new ReadOnlyDictionary<WorkActionKind, int>(actionCosts));
    }
}
