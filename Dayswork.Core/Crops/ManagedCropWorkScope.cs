namespace Dayswork.Core.Crops;

public sealed record ManagedCropWorkScope
{
    public IReadOnlyList<CropZoneAssignment> Assignments { get; }

    public ManagedCropWorkScope(IReadOnlyList<CropZoneAssignment>? assignments)
    {
        Assignments = (assignments ?? Array.Empty<CropZoneAssignment>())
            .Where(assignment => assignment.IsEnabled)
            .ToList()
            .AsReadOnly();
    }

    public bool IsEnabled => Assignments.Count > 0;
}
