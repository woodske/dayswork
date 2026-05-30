using Dayswork.Core.Compat;
using FsCheck.Xunit;
using Xunit;

namespace Dayswork.Tests.Compat;

public sealed class ExpansionProfileSelectorTests
{
    private static ExpansionProfileSelector NewSelector() =>
        new(new IExpansionProfile[] { new SveExpansionProfile(), new VanillaExpansionProfile() });

    [Fact]
    public void Selects_vanilla_for_empty_set()
    {
        Assert.Equal("vanilla", NewSelector().Select(new HashSet<string>()).Id);
    }

    [Fact]
    public void Selects_vanilla_when_only_unrelated_mods_present()
    {
        var profile = NewSelector().Select(new HashSet<string> { "Pathoschild.ContentPatcher" });
        Assert.Equal("vanilla", profile.Id);
    }

    [Fact]
    public void Selects_sve_when_content_id_present()
    {
        var profile = NewSelector().Select(new HashSet<string> { SveExpansionProfile.ContentModId });
        Assert.Equal("sve", profile.Id);
    }

    [Fact]
    public void Selects_sve_when_only_code_id_present()
    {
        var profile = NewSelector().Select(new HashSet<string> { SveExpansionProfile.CodeModId });
        Assert.Equal("sve", profile.Id);
    }

    [Property(Arbitrary = new[] { typeof(ExpansionCompatGenerators) }, MaxTest = 500)]
    public void Selection_is_deterministic_and_tracks_sve_presence(IReadOnlySet<string> installed)
    {
        var selector = NewSelector();

        var first = selector.Select(installed);
        var second = selector.Select(installed);
        Assert.Equal(first.Id, second.Id);

        var svePresent = installed.Contains(SveExpansionProfile.ContentModId)
                         || installed.Contains(SveExpansionProfile.CodeModId);
        Assert.Equal(svePresent ? "sve" : "vanilla", first.Id);
    }

    [Property(Arbitrary = new[] { typeof(ExpansionCompatGenerators) }, MaxTest = 500)]
    public void Always_selects_exactly_one_non_null_profile(IReadOnlySet<string> installed)
    {
        Assert.NotNull(NewSelector().Select(installed));
    }
}
