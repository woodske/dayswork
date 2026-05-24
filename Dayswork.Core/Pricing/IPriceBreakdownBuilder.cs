namespace Dayswork.Core.Pricing;

using Dayswork.Core.Config;
using Dayswork.Core.Domain;

public interface IPriceBreakdownBuilder
{
    PricingSnapshot BuildSnapshot(
        WorkScopeSet scopes,
        IReadOnlySet<TaskKind> enabledTasks,
        IReadOnlyList<OutdoorServiceBand> outdoorBands,
        ContractPriceTotals totals,
        IConfigSnapshot config);
}
