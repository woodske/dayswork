namespace Dayswork.Core.Pricing;

using Dayswork.Core.Config;
using Dayswork.Core.Domain;

public static class DepositHoursPolicy
{
    // Current v1 preview policy: SummaryMenu already presents a flat 1.0 billable hour because the
    // raw tile-based HoursEstimator is too large for gameplay. Recurring day-start deposits must use
    // the same policy so a confirmed contract cannot inflate on later mornings.
    public const double FlatPreviewHours = 1.0;

    public static double EstimateBillableHours(
        IEnumerable<Zone> zones,
        int numEnabledTasks,
        IConfigSnapshot config) =>
        FlatPreviewHours;
}
