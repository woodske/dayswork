namespace Dayswork.Core.Crops;

/// <summary>
/// A pickable fertilizer row in the Manage Crops authoring UI. A null selection represents
/// the "None" option (no fertilizer).
/// </summary>
public sealed record FertilizerOption(
    string ItemId,
    string DisplayName,
    CropSupplyTag Supply);
