using Dayswork.Core.Compat;
using Xunit;

namespace Dayswork.Tests.Compat;

/// <summary>
/// In U-SVE-01 both profiles must report "no override" for every lookup (Vanilla by design;
/// SVE because its override tables are empty until U-SVE-02..04). This guarantees no behavior
/// change in this unit (BR-SVE-05/07, S-21).
/// </summary>
public sealed class ExpansionProfileNoOpTests
{
    public static IEnumerable<object[]> Profiles() => new[]
    {
        new object[] { new VanillaExpansionProfile() },
        new object[] { new SveExpansionProfile() },
    };

    [Theory]
    [MemberData(nameof(Profiles))]
    public void Entrance_override_is_absent(IExpansionProfile profile)
    {
        Assert.False(profile.TryGetEntranceOverride(new FarmMapSignature(80, 65), out _));
    }

    [Theory]
    [MemberData(nameof(Profiles))]
    public void Content_override_is_absent(IExpansionProfile profile)
    {
        var hasOverride = profile.TryClassifyContentOverride(
            new ContentDescriptor(WorldContentKind.ResourceClump, "600"), out var result);

        Assert.False(hasOverride);
        Assert.Equal(WorkClassificationKind.None, result.Kind);
    }

    [Theory]
    [MemberData(nameof(Profiles))]
    public void Work_location_membership_is_false(IExpansionProfile profile)
    {
        Assert.False(profile.IsExpansionWorkLocation("Shed"));
    }

    [Theory]
    [MemberData(nameof(Profiles))]
    public void Premium_tier_mapping_is_null(IExpansionProfile profile)
    {
        Assert.Null(profile.MapPremiumBuildingTier("Barn"));
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
