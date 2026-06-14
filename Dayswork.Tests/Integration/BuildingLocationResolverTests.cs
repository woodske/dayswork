using System.Collections.Generic;
using Dayswork.Integration;
using Xunit;

namespace Dayswork.Tests.Integration;

public sealed class BuildingLocationResolverTests
{
    // Helper: a building whose interior name, unique name, indoors name, and type are all the same
    // (the single-building case) unless a distinct uniqueName/type is given.
    private static BuildingLocationResolver.BuildingMatchInfo B(string name, string? uniqueName = null, string? type = null) =>
        new(name, uniqueName ?? name, name, type ?? name);

    // Scenario from the SVE playtest: a base Coop/Barn alongside SVE Premium Coop/Barn, with the
    // premium buildings enumerating first. Before the exact-first fix, "Coop"/"Barn" loose-matched
    // the premium buildings (their type contains "Coop"/"Barn"), so the base buildings were never
    // serviced and the premium ones were serviced twice.
    private static IReadOnlyList<BuildingLocationResolver.BuildingMatchInfo> SvePlusBaseFarm() => new[]
    {
        B("FlashShifter.StardewValleyExpandedCP_PremiumCoop"),
        B("FlashShifter.StardewValleyExpandedCP_PremiumBarn"),
        B("Coop"),
        B("Barn"),
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
        var farm = new[] { B("Big Coop"), B("Deluxe Coop"), B("Coop") };
        Assert.Equal(expectedIndex, BuildingLocationResolver.SelectBuildingIndex(requestedName, farm));
    }

    [Theory]
    // Two same-type Coops share Name/type ("Coop") but have distinct unique interior names.
    // Each unique-name selection must resolve to its own building.
    [InlineData("Coop1bc97e5f", 0)]
    [InlineData("Coopd3136445", 1)]
    public void Resolves_duplicate_same_type_buildings_by_unique_name(string requestedName, int expectedIndex)
    {
        var farm = new[]
        {
            B("Coop", uniqueName: "Coop1bc97e5f", type: "Coop"),
            B("Coop", uniqueName: "Coopd3136445", type: "Coop"),
        };

        Assert.Equal(expectedIndex, BuildingLocationResolver.SelectBuildingIndex(requestedName, farm));
    }

    [Fact]
    public void Legacy_type_name_still_resolves_when_unique_names_differ()
    {
        // A legacy contract that stored the type name "Coop" still resolves (to the first such
        // building) via the exact type-name match — backward compatible.
        var farm = new[]
        {
            B("Coop", uniqueName: "Coop1bc97e5f", type: "Coop"),
            B("Coop", uniqueName: "Coopd3136445", type: "Coop"),
        };

        Assert.Equal(0, BuildingLocationResolver.SelectBuildingIndex("Coop", farm));
    }

    [Fact]
    public void Falls_back_to_loose_match_when_no_exact_match_exists()
    {
        // A display-name-ish request that doesn't exactly equal any interior/type name still resolves
        // via the loose fallback (preserves prior resilience).
        var farm = new[] { B("Deluxe Coop") };
        Assert.Equal(0, BuildingLocationResolver.SelectBuildingIndex("Coop", farm));
    }

    [Fact]
    public void Returns_negative_one_when_nothing_matches()
    {
        var farm = new[] { B("Barn") };
        Assert.Equal(-1, BuildingLocationResolver.SelectBuildingIndex("Greenhouse", farm));
    }
}
