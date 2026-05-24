namespace Dayswork.Core.Pricing;

using Dayswork.Core.Config;
using Dayswork.Core.Domain;

public interface IContractPriceCalculator
{
    ContractPriceTotals Calculate(
        WorkScopeSet scopes,
        IReadOnlySet<TaskKind> enabledTasks,
        IReadOnlyList<OutdoorServiceBand> outdoorBands,
        IConfigSnapshot config);
}
