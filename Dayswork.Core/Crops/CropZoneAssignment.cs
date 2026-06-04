namespace Dayswork.Core.Crops;

using Dayswork.Core.Domain;

public sealed record CropZoneAssignment
{
    public Zone Zone { get; }
    public CropAssignmentMode Mode { get; }
    public IReadOnlyList<SeasonCropChoice> Choices { get; }
    public ChestRef? OutputChest { get; }

    public CropZoneAssignment(
        Zone zone,
        CropAssignmentMode mode,
        IReadOnlyList<SeasonCropChoice>? choices,
        ChestRef? outputChest = null)
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
        OutputChest = outputChest;
    }

    public bool IsEnabled => Choices.Count > 0;
}
