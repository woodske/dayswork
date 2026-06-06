namespace Dayswork.Core.Crops;

using Dayswork.Core.Domain;

public sealed record SeasonCropChoice
{
    public Season Season { get; }
    public CropDescriptor Crop { get; }
    public StorePreference StorePreference { get; }
    public bool IsLocked { get; }
    public Season? OriginSeason { get; }

    /// <summary>
    /// When true, the farmhand refills this season's crop on emptied/empty prepared tiles each
    /// shift (spec §6.4). Authored in the Manage Crops UI (U-MC-03); consumed at runtime (U-MC-05).
    /// </summary>
    public bool AutoReplant { get; }

    public SeasonCropChoice(
        Season season,
        CropDescriptor crop,
        StorePreference storePreference = StorePreference.Either,
        bool isLocked = false,
        Season? originSeason = null,
        bool autoReplant = false)
    {
        Season = season;
        Crop = crop;
        StorePreference = storePreference;
        IsLocked = isLocked;
        OriginSeason = originSeason;
        AutoReplant = autoReplant;
    }
}
