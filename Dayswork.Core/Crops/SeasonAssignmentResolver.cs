namespace Dayswork.Core.Crops;

using Dayswork.Core.Domain;

public sealed class SeasonAssignmentResolver
{
    public CropZoneAssignment ApplyChoice(CropZoneAssignment assignment, SeasonCropChoice choice)
    {
        if (assignment.Mode == CropAssignmentMode.SeasonAgnostic)
        {
            return new CropZoneAssignment(
                assignment.Zone,
                assignment.Mode,
                new[] { new SeasonCropChoice(choice.Season, choice.Crop, choice.StorePreference) },
                assignment.OutputChest);
        }

        var replacements = BuildSeasonalChoices(choice).ToDictionary(item => item.Season);
        var result = assignment.Choices
            .Where(existing => !replacements.ContainsKey(existing.Season)
                && existing.OriginSeason != choice.Season)
            .Concat(replacements.Values)
            .OrderBy(existing => existing.Season)
            .ToList()
            .AsReadOnly();

        return new CropZoneAssignment(assignment.Zone, assignment.Mode, result, assignment.OutputChest);
    }

    public IReadOnlyList<SeasonCropChoice> BuildSeasonalChoices(SeasonCropChoice originChoice)
    {
        var cropSeasons = originChoice.Crop.Seasons.Count == 0
            ? new[] { originChoice.Season }
            : originChoice.Crop.Seasons;

        if (!cropSeasons.Contains(originChoice.Season))
            cropSeasons = new[] { originChoice.Season };

        return cropSeasons
            .OrderBy(season => season)
            .Select(season => new SeasonCropChoice(
                season,
                originChoice.Crop,
                originChoice.StorePreference,
                isLocked: season != originChoice.Season,
                originSeason: season == originChoice.Season ? null : originChoice.Season))
            .ToList()
            .AsReadOnly();
    }
}
