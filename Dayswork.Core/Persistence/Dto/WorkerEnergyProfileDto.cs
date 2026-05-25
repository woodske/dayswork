namespace Dayswork.Core.Persistence.Dto;

public sealed class WorkerEnergyProfileDto
{
    public int DailyCapacity { get; set; }
    public SortedDictionary<string, int> ActionCosts { get; set; } = new(StringComparer.Ordinal);
}
