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
    /// shift. Authored in the Manage Crops UI; consumed at runtime.
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
