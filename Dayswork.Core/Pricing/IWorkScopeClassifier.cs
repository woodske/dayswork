namespace Dayswork.Core.Pricing;

using Dayswork.Core.Crops;
using Dayswork.Core.Domain;

public interface IWorkScopeClassifier
{
    WorkScopeSet Classify(ContractScopeSelection selection, IReadOnlySet<TaskKind> enabledTasks, CropPlan? cropPlan = null);
}
