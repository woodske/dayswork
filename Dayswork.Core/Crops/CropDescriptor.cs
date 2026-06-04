namespace Dayswork.Core.Crops;

using Dayswork.Core.Domain;

public sealed record CropDescriptor
{
    public string CropItemId { get; }
    public string SeedItemId { get; }
    public string? FertilizerItemId { get; }
    public int DaysToFirstHarvest { get; }
    public int? FertilizedDaysToFirstHarvest { get; }
    public int? RegrowDays { get; }
    public IReadOnlyList<Season> Seasons { get; }

    public CropDescriptor(
        string cropItemId,
        string seedItemId,
        string? fertilizerItemId,
        int daysToFirstHarvest,
        int? fertilizedDaysToFirstHarvest,
        int? regrowDays,
        IReadOnlyList<Season>? seasons)
    {
        CropItemId = cropItemId;
        SeedItemId = seedItemId;
        FertilizerItemId = string.IsNullOrWhiteSpace(fertilizerItemId) ? null : fertilizerItemId;
        DaysToFirstHarvest = Math.Max(1, daysToFirstHarvest);
        FertilizedDaysToFirstHarvest = fertilizedDaysToFirstHarvest is null
            ? null
            : Math.Max(1, fertilizedDaysToFirstHarvest.Value);
        RegrowDays = regrowDays is null ? null : Math.Max(1, regrowDays.Value);
        Seasons = (seasons ?? Array.Empty<Season>())
            .Distinct()
            .OrderBy(season => season)
            .ToList()
            .AsReadOnly();
    }

    public bool RequiresFertilizer => FertilizerItemId is not null;
    public bool IsMultiSeason => Seasons.Count > 1;

    public int EffectiveDaysToFirstHarvest(bool useFertilizer) =>
        useFertilizer && FertilizedDaysToFirstHarvest is not null
            ? FertilizedDaysToFirstHarvest.Value
            : DaysToFirstHarvest;
}
