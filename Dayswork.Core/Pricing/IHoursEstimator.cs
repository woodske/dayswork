namespace Dayswork.Core.Pricing;

using Dayswork.Core.Config;
using Dayswork.Core.Domain;

public interface IHoursEstimator
{
    double Estimate(IEnumerable<Zone> zones, int numEnabledTasks, IConfigSnapshot config);
}
