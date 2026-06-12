namespace Dayswork.Core.Crops;

using Dayswork.Core.Domain;
using Dayswork.Core.Persistence.Dto;
using Newtonsoft.Json;

public static class CropPlanSerialization
{
    public static CropPlanDtoV1? MapDomainToDto(CropPlan cropPlan)
    {
        if (!cropPlan.IsEnabled)
            return null;

        return new CropPlanDtoV1
        {
            BuyFromJojaFirst = cropPlan.BuyFromJojaFirst,
            Assignments = cropPlan.Assignments
                .OrderBy(DescribeAssignment, StringComparer.Ordinal)
                .Select(MapAssignmentToDto)
                .ToList(),
        };
    }

    public static CropPlan MapDtoToDomain(CropPlanDtoV1? dto)
    {
        if (dto is null || dto.Assignments.Count == 0)
            return CropPlan.Empty;

        return new CropPlan(dto.Assignments.Select(MapAssignmentToDomain).ToList(), dto.BuyFromJojaFirst);
    }

    private static CropZoneAssignmentDtoV1 MapAssignmentToDto(CropZoneAssignment assignment) =>
        new()
        {
            LocationName = assignment.Zone.LocationName,
            TopLeftX = assignment.Zone.TopLeft.X,
            TopLeftY = assignment.Zone.TopLeft.Y,
            BottomRightX = assignment.Zone.BottomRight.X,
            BottomRightY = assignment.Zone.BottomRight.Y,
            Mode = assignment.Mode.ToString(),
            Choices = assignment.Choices
                .OrderBy(choice => choice.Season)
                .ThenBy(choice => choice.IsLocked)
                .Select(MapChoiceToDto)
                .ToList(),
            OutputChest = assignment.OutputChest is null ? null : MapChestRefToDto(assignment.OutputChest),
            GroupId = assignment.GroupId,
        };

    private static CropZoneAssignment MapAssignmentToDomain(CropZoneAssignmentDtoV1 dto)
    {
        if (string.IsNullOrWhiteSpace(dto.LocationName))
            throw new JsonException("CropPlan assignment LocationName was null or empty.");

        return new CropZoneAssignment(
            new Zone(
                dto.LocationName,
                new TileCoord(dto.TopLeftX, dto.TopLeftY),
                new TileCoord(dto.BottomRightX, dto.BottomRightY)),
            Enum.Parse<CropAssignmentMode>(Require(dto.Mode, "CropPlan assignment Mode")),
            (dto.Choices ?? throw new JsonException("CropPlan assignment Choices was null."))
                .Select(MapChoiceToDomain)
                .ToList(),
            dto.OutputChest is null ? null : MapChestRefToDomain(dto.OutputChest),
            dto.GroupId);
    }

    private static SeasonCropChoiceDtoV1 MapChoiceToDto(SeasonCropChoice choice) =>
        new()
        {
            Season = choice.Season.ToString(),
            CropItemId = choice.Crop.CropItemId,
            SeedItemId = choice.Crop.SeedItemId,
            FertilizerItemId = choice.Crop.FertilizerItemId,
            DaysToFirstHarvest = choice.Crop.DaysToFirstHarvest,
            FertilizedDaysToFirstHarvest = choice.Crop.FertilizedDaysToFirstHarvest,
            RegrowDays = choice.Crop.RegrowDays,
            CropSeasons = choice.Crop.Seasons.Select(season => season.ToString()).ToList(),
            StorePreference = choice.StorePreference.ToString(),
            IsLocked = choice.IsLocked,
            OriginSeason = choice.OriginSeason?.ToString(),
        };

    private static SeasonCropChoice MapChoiceToDomain(SeasonCropChoiceDtoV1 dto)
    {
        var crop = new CropDescriptor(
            Require(dto.CropItemId, "CropPlan choice CropItemId"),
            Require(dto.SeedItemId, "CropPlan choice SeedItemId"),
            dto.FertilizerItemId,
            dto.DaysToFirstHarvest,
            dto.FertilizedDaysToFirstHarvest,
            dto.RegrowDays,
            (dto.CropSeasons ?? throw new JsonException("CropPlan choice CropSeasons was null."))
                .Select(season => Enum.Parse<Season>(season))
                .ToList());

        return new SeasonCropChoice(
            Enum.Parse<Season>(Require(dto.Season, "CropPlan choice Season")),
            crop,
            Enum.Parse<StorePreference>(Require(dto.StorePreference, "CropPlan choice StorePreference")),
            dto.IsLocked,
            string.IsNullOrWhiteSpace(dto.OriginSeason) ? null : Enum.Parse<Season>(dto.OriginSeason));
    }

    private static ChestRefDtoV1 MapChestRefToDto(ChestRef chestRef) =>
        new()
        {
            LocationName = chestRef.LocationName,
            X = chestRef.Tile.X,
            Y = chestRef.Tile.Y,
        };

    private static ChestRef MapChestRefToDomain(ChestRefDtoV1 dto) =>
        new(Require(dto.LocationName, "CropPlan OutputChest LocationName"), new TileCoord(dto.X, dto.Y));

    private static string Require(string? value, string name) =>
        string.IsNullOrWhiteSpace(value) ? throw new JsonException($"{name} was null or empty.") : value;

    private static string DescribeAssignment(CropZoneAssignment assignment) =>
        $"{assignment.Zone.LocationName}|{assignment.Zone.TopLeft.X}|{assignment.Zone.TopLeft.Y}|{assignment.Zone.BottomRight.X}|{assignment.Zone.BottomRight.Y}";
}
