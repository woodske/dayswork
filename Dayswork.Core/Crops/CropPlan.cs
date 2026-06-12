namespace Dayswork.Core.Crops;

public sealed record CropPlan
{
    public static CropPlan Empty { get; } = new(Array.Empty<CropZoneAssignment>());

    public IReadOnlyList<CropZoneAssignment> Assignments { get; }
    public bool BuyFromJojaFirst { get; }

    public CropPlan(IReadOnlyList<CropZoneAssignment>? assignments, bool buyFromJojaFirst = false)
    {
        Assignments = (assignments ?? Array.Empty<CropZoneAssignment>())
            .Where(assignment => assignment.IsEnabled)
            .OrderBy(DescribeAssignment, StringComparer.Ordinal)
            .ToList()
            .AsReadOnly();
        BuyFromJojaFirst = buyFromJojaFirst;
    }

    public bool IsEnabled => Assignments.Count > 0;

    private static string DescribeAssignment(CropZoneAssignment assignment) =>
        $"{assignment.Zone.LocationName}|{assignment.Zone.TopLeft.X}|{assignment.Zone.TopLeft.Y}|{assignment.Zone.BottomRight.X}|{assignment.Zone.BottomRight.Y}";
}
