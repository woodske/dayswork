namespace Dayswork.Core.Crops;

public sealed record ManagedCropWorkScope
{
    public IReadOnlyList<CropZoneAssignment> Assignments { get; }
    public bool BuyFromJojaFirst { get; }

    public ManagedCropWorkScope(IReadOnlyList<CropZoneAssignment>? assignments, bool buyFromJojaFirst = false)
    {
        Assignments = (assignments ?? Array.Empty<CropZoneAssignment>())
            .Where(assignment => assignment.IsEnabled)
            .ToList()
            .AsReadOnly();
        BuyFromJojaFirst = buyFromJojaFirst;
    }

    public bool IsEnabled => Assignments.Count > 0;
}
