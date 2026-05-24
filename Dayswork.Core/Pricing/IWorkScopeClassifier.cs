namespace Dayswork.Core.Pricing;

using Dayswork.Core.Domain;

public interface IWorkScopeClassifier
{
    WorkScopeSet Classify(ContractScopeSelection selection, IReadOnlySet<TaskKind> enabledTasks);
}
