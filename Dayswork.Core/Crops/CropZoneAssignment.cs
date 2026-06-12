namespace Dayswork.Core.Crops;

using Dayswork.Core.Domain;

public sealed record CropZoneAssignment
{
    public Zone Zone { get; }
    public CropAssignmentMode Mode { get; }
    public IReadOnlyList<SeasonCropChoice> Choices { get; }
    public DestinationKey? OutputDestination { get; }
    public string? GroupId { get; }

    public CropZoneAssignment(
        Zone zone,
        CropAssignmentMode mode,
        IReadOnlyList<SeasonCropChoice>? choices,
        DestinationKey? outputDestination = null,
        string? groupId = null)
    {
        Zone = zone;
        Mode = mode;
        Choices = (choices ?? Array.Empty<SeasonCropChoice>())
            .OrderBy(choice => choice.Season)
            .ThenBy(choice => choice.IsLocked)
            .ThenBy(choice => choice.Crop.CropItemId, StringComparer.Ordinal)
            .ThenBy(choice => choice.Crop.SeedItemId, StringComparer.Ordinal)
            .ToList()
            .AsReadOnly();
        OutputDestination = outputDestination;
        GroupId = string.IsNullOrWhiteSpace(groupId) ? null : groupId;
    }

    public bool IsEnabled => Choices.Count > 0;
}
