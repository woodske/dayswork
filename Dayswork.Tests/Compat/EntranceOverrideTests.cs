using Dayswork.Core.Compat;
using FsCheck.Xunit;
using Xunit;

namespace Dayswork.Tests.Compat;

public sealed class EntranceOverrideTests
{
    [Fact]
    public void Vanilla_has_no_entrance_override()
    {
        var p = new VanillaExpansionProfile();
        Assert.False(p.TryGetEntranceOverride(new FarmMapSignature(163, 156), out _)); // IF2R
        Assert.False(p.TryGetEntranceOverride(new FarmMapSignature(156, 65), out _));  // Frontier
    }

    [Fact]
    public void Sve_entrance_table_is_empty_pending_playtest()
    {
        // U-SVE-02 ships the mechanism; per-farm entrance overrides are added only after playtest
        // confirms the warp heuristic is wrong. Until then every lookup falls through (no override).
        var p = new SveExpansionProfile();
        Assert.False(p.TryGetEntranceOverride(new FarmMapSignature(163, 156), out _)); // IF2R
        Assert.False(p.TryGetEntranceOverride(new FarmMapSignature(156, 65), out _));  // Frontier
    }

    [Fact]
    public void Signature_value_equality_supports_dictionary_keying()
    {
        Assert.Equal(new FarmMapSignature(163, 156), new FarmMapSignature(163, 156, string.Empty));
        Assert.NotEqual(new FarmMapSignature(163, 156), new FarmMapSignature(156, 163));
        Assert.NotEqual(new FarmMapSignature(163, 156, "a"), new FarmMapSignature(163, 156, "b"));
    }

    [Property(MaxTest = 300)]
    public void Vanilla_override_is_always_false(int width, int height)
    {
        Assert.False(new VanillaExpansionProfile().TryGetEntranceOverride(new FarmMapSignature(width, height), out _));
    }

    [Property(MaxTest = 300)]
    public void Sve_lookup_is_deterministic(int width, int height)
    {
        var p = new SveExpansionProfile();
        var sig = new FarmMapSignature(width, height);
        Assert.Equal(p.TryGetEntranceOverride(sig, out _), p.TryGetEntranceOverride(sig, out _));
    }
}
