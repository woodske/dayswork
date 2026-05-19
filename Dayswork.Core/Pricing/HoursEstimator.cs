namespace Dayswork.Core.Pricing;

using Dayswork.Core.Config;
using Dayswork.Core.Domain;

public sealed class HoursEstimator : IHoursEstimator
{
    public double Estimate(IEnumerable<Zone> zones, int numEnabledTasks, IConfigSnapshot config)
    {
        var totalTiles = 0;

        foreach (var zone in zones)
        {
            var width  = zone.BottomRight.X - zone.TopLeft.X + 1;
            var height = zone.BottomRight.Y - zone.TopLeft.Y + 1;
            totalTiles += width * height;
        }

        return (totalTiles * numEnabledTasks * config.AverageSpeedConstant) / 60.0;
    }
}
