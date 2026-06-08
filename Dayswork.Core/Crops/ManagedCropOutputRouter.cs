namespace Dayswork.Core.Crops;

using Dayswork.Core.Domain;

public static class ManagedCropOutputRouter
{
    public static string AssignmentKey(CropZoneAssignment assignment)
    {
        if (assignment is null) throw new ArgumentNullException(nameof(assignment));

        var zone = assignment.Zone;
        return string.Join(
            "|",
            Part(assignment.GroupId),
            Part(zone.LocationName),
            zone.TopLeft.X.ToString(System.Globalization.CultureInfo.InvariantCulture),
            zone.TopLeft.Y.ToString(System.Globalization.CultureInfo.InvariantCulture),
            zone.BottomRight.X.ToString(System.Globalization.CultureInfo.InvariantCulture),
            zone.BottomRight.Y.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    public static OutputScopeProvenance ProvenanceFor(CropZoneAssignment assignment) =>
        OutputScopeProvenance.ManagedCrop(AssignmentKey(assignment));

    public static IReadOnlyDictionary<OutputScopeProvenance, DestinationKey> BuildDestinationMap(
        IEnumerable<CropZoneAssignment>? assignments)
    {
        if (assignments is null)
            return new Dictionary<OutputScopeProvenance, DestinationKey>();

        return assignments
            .Where(assignment => assignment.OutputChest is not null)
            .Select(assignment => new
            {
                Provenance = ProvenanceFor(assignment),
                Destination = new ChestDestination(assignment.OutputChest!)
            })
            .GroupBy(entry => entry.Provenance)
            .ToDictionary(
                group => group.Key,
                group => (DestinationKey)group
                    .OrderBy(entry => entry.Destination.Ref.LocationName, StringComparer.Ordinal)
                    .ThenBy(entry => entry.Destination.Ref.Tile.X)
                    .ThenBy(entry => entry.Destination.Ref.Tile.Y)
                    .First()
                    .Destination);
    }

    private static string Part(string? value)
    {
        var text = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        return text.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + text;
    }
}
