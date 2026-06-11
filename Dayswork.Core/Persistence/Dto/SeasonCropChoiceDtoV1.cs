namespace Dayswork.Core.Persistence.Dto;

public sealed class SeasonCropChoiceDtoV1
{
    public string Season { get; set; } = "";
    public string CropItemId { get; set; } = "";
    public string SeedItemId { get; set; } = "";
    public string? FertilizerItemId { get; set; }
    public int DaysToFirstHarvest { get; set; }
    public int? FertilizedDaysToFirstHarvest { get; set; }
    public int? RegrowDays { get; set; }
    public List<string> CropSeasons { get; set; } = new();
    public string StorePreference { get; set; } = "";
    public bool IsLocked { get; set; }
    public string? OriginSeason { get; set; }
    public bool? AutoReplant { get; set; } // obsolete — kept for safe deserialization of old saves
}
