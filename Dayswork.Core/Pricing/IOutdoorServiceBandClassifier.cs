namespace Dayswork.Core.Pricing;

using Dayswork.Core.Config;
using Dayswork.Core.Domain;

public interface IOutdoorServiceBandClassifier
{
    IReadOnlyList<OutdoorServiceBand> ClassifyBands(
        WorkScopeSet scopes,
        IReadOnlySet<TaskKind> enabledTasks,
        IConfigSnapshot config);
}
