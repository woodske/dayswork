using System.Text.Json;
using Dayswork.Core.Domain;
using Xunit;

namespace Dayswork.Tests.Domain;

public class WorkerAppearanceTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("a-palette-that-was-retired")]
    public void Resolve_falls_back_to_the_default_palette(string? key)
    {
        // A save can carry an unset or long-gone appearance key; it must never leave a worker
        // without a sprite to load.
        var appearance = WorkerAppearances.Resolve(key);

        Assert.Equal(WorkerAppearances.DefaultKey, appearance.Key);
        Assert.False(appearance.HasTint);
    }

    [Fact]
    public void Resolve_is_case_insensitive()
    {
        Assert.Equal("forest", WorkerAppearances.Resolve("FOREST").Key);
    }

    [Fact]
    public void The_default_palette_is_the_only_untinted_one()
    {
        Assert.Equal(
            new[] { WorkerAppearances.DefaultKey },
            WorkerAppearances.All.Where(a => !a.HasTint).Select(a => a.Key));
    }

    [Fact]
    public void Palette_keys_are_unique_and_non_empty()
    {
        Assert.All(WorkerAppearances.All, a => Assert.False(string.IsNullOrWhiteSpace(a.Key)));
        Assert.Equal(
            WorkerAppearances.All.Count,
            WorkerAppearances.All.Select(a => a.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void Tints_stay_inside_the_ranges_the_game_painter_accepts()
    {
        foreach (var tint in WorkerAppearances.All
                     .SelectMany(a => new[] { a.Cap, a.Shirt, a.Overalls })
                     .OfType<AppearanceTint>())
        {
            Assert.InRange(tint.Hue, 0, 359);
            Assert.InRange(tint.Saturation, 0, 100);
            Assert.InRange(tint.Lightness, -100, 100);
        }
    }

    [Fact]
    public void NextKey_walks_the_whole_list_and_wraps()
    {
        var visited = new List<string>();
        var key = WorkerAppearances.DefaultKey;

        for (var i = 0; i < WorkerAppearances.All.Count; i++)
        {
            visited.Add(key);
            key = WorkerAppearances.NextKey(key);
        }

        Assert.Equal(WorkerAppearances.All.Select(a => a.Key), visited);
        Assert.Equal(WorkerAppearances.DefaultKey, key);
    }

    [Fact]
    public void PreviousKey_is_the_inverse_of_NextKey()
    {
        foreach (var appearance in WorkerAppearances.All)
            Assert.Equal(appearance.Key, WorkerAppearances.PreviousKey(WorkerAppearances.NextKey(appearance.Key)));
    }

    [Fact]
    public void PreviousKey_from_the_default_wraps_to_the_last_palette()
    {
        Assert.Equal(
            WorkerAppearances.All[^1].Key,
            WorkerAppearances.PreviousKey(WorkerAppearances.DefaultKey));
    }

    [Fact]
    public void Every_palette_has_a_translation()
    {
        using var stream = File.OpenRead(Path.Combine(FindWorkspaceRoot(), "Dayswork", "i18n", "default.json"));
        using var i18n = JsonDocument.Parse(stream);

        foreach (var appearance in WorkerAppearances.All)
        {
            Assert.True(
                i18n.RootElement.TryGetProperty($"ui.preferences.appearance.{appearance.Key}", out _),
                $"Palette '{appearance.Key}' has no ui.preferences.appearance.* entry in i18n/default.json.");
        }
    }

    private static string FindWorkspaceRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AGENTS.md")))
            dir = dir.Parent;

        Assert.NotNull(dir);
        return dir!.FullName;
    }
}
