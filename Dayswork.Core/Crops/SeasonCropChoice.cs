namespace Dayswork.Core.Crops;

using Dayswork.Core.Domain;

public sealed record SeasonCropChoice
{
    public Season Season { get; }
    public CropDescriptor Crop { get; }
    public StorePreference StorePreference { get; }
    public bool IsLocked { get; }
    public Season? OriginSeason { get; }

    public SeasonCropChoice(
        Season season,
        CropDescriptor crop,
        StorePreference storePreference = StorePreference.Either,
        bool isLocked = false,
        Season? originSeason = null)
    {
        Season = season;
        Crop = crop;
        StorePreference = storePreference;
        IsLocked = isLocked;
        OriginSeason = originSeason;
    }
}
