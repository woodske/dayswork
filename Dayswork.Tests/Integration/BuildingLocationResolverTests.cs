using System.Collections.Generic;
using Dayswork.Integration;
using Xunit;

namespace Dayswork.Tests.Integration;

public sealed class BuildingLocationResolverTests
{
    // Scenario from the SVE playtest: a base Coop/Barn alongside SVE Premium Coop/Barn, with the
    // premium buildings enumerating first. Before the exact-first fix, "Coop"/"Barn" loose-matched
    // the premium buildings (their type contains "Coop"/"Barn"), so the base buildings were never
    // serviced and the premium ones were serviced twice.
    private static IReadOnlyList<BuildingLocationResolver.BuildingMatchInfo> SvePlusBaseFarm() => new[]
    {
        new BuildingLocationResolver.BuildingMatchInfo("FlashShifter.StardewValleyExpandedCP_PremiumCoop", "FlashShifter.StardewValleyExpandedCP_PremiumCoop", "FlashShifter.StardewValleyExpandedCP_PremiumCoop"),
        new BuildingLocationResolver.BuildingMatchInfo("FlashShifter.StardewValleyExpandedCP_PremiumBarn", "FlashShifter.StardewValleyExpandedCP_PremiumBarn", "FlashShifter.StardewValleyExpandedCP_PremiumBarn"),
        new BuildingLocationResolver.BuildingMatchInfo("Coop", "Coop", "Coop"),
        new BuildingLocationResolver.BuildingMatchInfo("Barn", "Barn", "Barn"),
    };

    [Theory]
    [InlineData("Coop", 2)]                                                       // base coop, not premium (idx 0)
    [InlineData("Barn", 3)]                                                       // base barn, not premium (idx 1)
    [InlineData("FlashShifter.StardewValleyExpandedCP_PremiumCoop", 0)]
    [InlineData("FlashShifter.StardewValleyExpandedCP_PremiumBarn", 1)]
    public void Resolves_each_selection_to_its_own_building(string requestedName, int expectedIndex)
    {
        Assert.Equal(expectedIndex, BuildingLocationResolver.SelectBuildingIndex(requestedName, SvePlusBaseFarm()));
    }

    [Theory]
    // Vanilla same-family buildings: "Coop" must not resolve to "Big Coop"/"Deluxe Coop" via loose
    // substring matching when an exact "Coop" exists.
    [InlineData("Coop", 2)]
    [InlineData("Big Coop", 0)]
    [InlineData("Deluxe Coop", 1)]
    public void Exact_match_wins_over_loose_substring_for_vanilla_tiers(string requestedName, int expectedIndex)
    {
        var farm = new[]
        {
            new BuildingLocationResolver.BuildingMatchInfo("Big Coop", "Big Coop", "Big Coop"),
            new BuildingLocationResolver.BuildingMatchInfo("Deluxe Coop", "Deluxe Coop", "Deluxe Coop"),
            new BuildingLocationResolver.BuildingMatchInfo("Coop", "Coop", "Coop"),
        };

        Assert.Equal(expectedIndex, BuildingLocationResolver.SelectBuildingIndex(requestedName, farm));
    }

    [Fact]
    public void Falls_back_to_loose_match_when_no_exact_match_exists()
    {
        // A display-name-ish request that doesn't exactly equal any interior/type name still resolves
        // via the loose fallback (preserves prior resilience).
        var farm = new[]
        {
            new BuildingLocationResolver.BuildingMatchInfo("Deluxe Coop", "Deluxe Coop", "Deluxe Coop"),
        };

        Assert.Equal(0, BuildingLocationResolver.SelectBuildingIndex("Coop", farm));
    }

    [Fact]
    public void Returns_negative_one_when_nothing_matches()
    {
        var farm = new[]
        {
            new BuildingLocationResolver.BuildingMatchInfo("Barn", "Barn", "Barn"),
        };

        Assert.Equal(-1, BuildingLocationResolver.SelectBuildingIndex("Greenhouse", farm));
    }
}
