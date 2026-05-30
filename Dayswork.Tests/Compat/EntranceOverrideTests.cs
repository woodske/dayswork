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

    [Theory]
    // Per-farm entrance overrides are added only after playtest confirms the warp heuristic lands
    // the worker on the wrong tile. Each configured tile is a preference — the orchestrator searches
    // outward for the nearest passable tile if it is blocked at spawn time.
    [InlineData(140, 93, 112, 51)] // Grandpa's Farm
    [InlineData(156, 65, 142, 16)] // Frontier Farm
    public void Sve_entrance_override_is_configured(int width, int height, int expectedX, int expectedY)
    {
        var p = new SveExpansionProfile();

        Assert.True(p.TryGetEntranceOverride(new FarmMapSignature(width, height), out var tile));
        Assert.Equal(expectedX, tile.X);
        Assert.Equal(expectedY, tile.Y);
    }

    [Fact]
    public void Sve_maps_without_a_confirmed_override_fall_through_to_the_heuristic()
    {
        // Maps whose warp heuristic already works correctly need no entry and fall through (no override).
        var p = new SveExpansionProfile();
        Assert.False(p.TryGetEntranceOverride(new FarmMapSignature(163, 156), out _)); // IF2R
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
