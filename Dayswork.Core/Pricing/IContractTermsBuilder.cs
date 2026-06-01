namespace Dayswork.Core.Pricing;

using Dayswork.Core.Config;
using Dayswork.Core.Domain;

public interface IContractTermsBuilder
{
    ContractTermsSnapshot BuildTerms(
        ContractScopeSelection selection,
        IReadOnlySet<TaskKind> enabledTasks,
        IConfigSnapshot config);

    ContractPreview BuildPreview(
        ContractScopeSelection selection,
        IReadOnlySet<TaskKind> enabledTasks,
        IConfigSnapshot config);
}
