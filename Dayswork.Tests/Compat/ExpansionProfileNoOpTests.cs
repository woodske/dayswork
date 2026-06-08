using Dayswork.Core.Compat;
using Xunit;

namespace Dayswork.Tests.Compat;

/// <summary>
/// Vanilla remains the Null-Object profile: every lookup reports "no override" so vanilla
/// behavior does not change when expansion compatibility seams are called.
/// </summary>
public sealed class ExpansionProfileNoOpTests
{
    public static IEnumerable<object[]> Profiles() => new[]
    {
        new object[] { new VanillaExpansionProfile() },
    };

    [Theory]
    [MemberData(nameof(Profiles))]
    public void Entrance_override_is_absent(IExpansionProfile profile)
    {
        Assert.False(profile.TryGetEntranceOverride(new FarmMapSignature(80, 65), out _));
    }

    [Theory]
    [MemberData(nameof(Profiles))]
    public void Premium_tier_mapping_is_null(IExpansionProfile profile)
    {
        Assert.Null(profile.MapPremiumBuildingTier("Barn"));
    }

    [Theory]
    [MemberData(nameof(Profiles))]
    public void Route_and_descriptor_lookups_are_empty(IExpansionProfile profile)
    {
        Assert.Empty(profile.GetLocationDescriptors());
        Assert.False(profile.TryGetRoute(
            new ExpansionRouteRequest(
                new FarmMapSignature(140, 93),
                "Farm",
                SveExpansionProfile.GrandpasShedGreenhouseLocation,
                ExpansionRoutePurpose.WorkEntry),
            out _));
    }

    [Fact]
    public void Vanilla_matches_any_installed_set()
    {
        Assert.True(new VanillaExpansionProfile().Matches(new HashSet<string>()));
        Assert.True(new VanillaExpansionProfile().Matches(new HashSet<string> { "anything" }));
    }

    [Fact]
    public void Sve_does_not_match_without_its_ids()
    {
        Assert.False(new SveExpansionProfile().Matches(new HashSet<string> { "Pathoschild.ContentPatcher" }));
    }
}
