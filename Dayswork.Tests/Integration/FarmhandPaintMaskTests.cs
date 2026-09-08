using Xunit;

namespace Dayswork.Tests.Integration;

/// <summary>
/// The game's <c>BuildingPainter</c> maps mask pixels onto the sheet by flat pixel index and matches
/// their colours by exact equality, so a mask that is not exactly the same size as the sheet — or
/// whose reds are 254 rather than 255 — paints the wrong pixels, or none, silently and with no
/// error. This guards that pairing if either asset is ever redrawn.
/// </summary>
public class FarmhandPaintMaskTests
{
    private static readonly (byte, byte, byte, byte) Transparent = (0, 0, 0, 0);
    private static readonly (byte, byte, byte, byte) Cap = (255, 0, 0, 255);      // Color.Red
    private static readonly (byte, byte, byte, byte) Shirt = (0, 255, 0, 255);    // Color.Lime
    private static readonly (byte, byte, byte, byte) Overalls = (0, 0, 255, 255); // Color.Blue

    [Fact]
    public void Paint_mask_matches_the_farmhand_sheet_dimensions()
    {
        var assets = Assets();

        Assert.Equal(
            PaintMaskAsset.ReadSize(Path.Combine(assets, "farmhand.png")),
            PaintMaskAsset.ReadSize(Path.Combine(assets, "farmhand_PaintMask.png")));
    }

    [Fact]
    public void Paint_mask_uses_only_the_three_exact_region_colours()
    {
        var colors = PaintMaskAsset.DistinctColors(Path.Combine(Assets(), "farmhand_PaintMask.png"));

        Assert.Empty(colors.Except(new[] { Transparent, Cap, Shirt, Overalls }));
        Assert.Contains(Cap, colors);
        Assert.Contains(Shirt, colors);
        Assert.Contains(Overalls, colors);
    }

    private static string Assets()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AGENTS.md")))
            dir = dir.Parent;

        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "Dayswork", "assets");
    }
}
