namespace Dayswork.Tests.ManageCrops;

using Dayswork.Core.Crops;
using Dayswork.Core.Domain;
using Xunit;

public sealed class SeasonAssignmentResolverTests
{
    [Fact]
    public void ApplyChoice_MultiSeasonCrop_CreatesLockedDerivedSeasons()
    {
        var resolver = new SeasonAssignmentResolver();
        var crop = new CropDescriptor("crop.corn", "seed.corn", null, 14, null, 4, new[] { Season.Summer, Season.Fall });
        var assignment = EmptySeasonalAssignment();

        var result = resolver.ApplyChoice(assignment, new SeasonCropChoice(Season.Summer, crop));

        Assert.Equal(2, result.Choices.Count);
        Assert.Contains(result.Choices, choice => choice.Season == Season.Summer && !choice.IsLocked);
        Assert.Contains(result.Choices, choice => choice.Season == Season.Fall && choice.IsLocked && choice.OriginSeason == Season.Summer);
    }

    [Fact]
    public void ApplyChoice_SameChoiceTwice_IsIdempotent()
    {
        var resolver = new SeasonAssignmentResolver();
        var crop = new CropDescriptor("crop.corn", "seed.corn", null, 14, null, 4, new[] { Season.Summer, Season.Fall });
        var choice = new SeasonCropChoice(Season.Summer, crop);

        var once = resolver.ApplyChoice(EmptySeasonalAssignment(), choice);
        var twice = resolver.ApplyChoice(once, choice);

        Assert.Equal(once.Choices, twice.Choices);
    }

    [Fact]
    public void ApplyChoice_SeasonAgnosticAssignment_StoresSingleUnlockedChoice()
    {
        var resolver = new SeasonAssignmentResolver();
        var crop = new CropDescriptor("crop.ancient", "seed.ancient", null, 28, null, 7, new[] { Season.Spring, Season.Summer, Season.Fall });
        var assignment = new CropZoneAssignment(
            new Zone("Greenhouse", new TileCoord(0, 0), new TileCoord(1, 1)),
            CropAssignmentMode.SeasonAgnostic,
            Array.Empty<SeasonCropChoice>());

        var result = resolver.ApplyChoice(assignment, new SeasonCropChoice(Season.Spring, crop));

        var onlyChoice = Assert.Single(result.Choices);
        Assert.False(onlyChoice.IsLocked);
    }

    private static CropZoneAssignment EmptySeasonalAssignment() =>
        new(
            new Zone("Farm", new TileCoord(0, 0), new TileCoord(1, 1)),
            CropAssignmentMode.Seasonal,
            Array.Empty<SeasonCropChoice>());
}
