using System.Buffers.Binary;
using Xunit;

namespace Dayswork.Tests.Integration;

/// <summary>
/// The game's <c>BuildingPainter</c> maps mask pixels onto the sheet by flat pixel index, so a mask
/// that is not exactly the same size as the sheet paints the wrong pixels — silently, with no error.
/// This guards that pairing if either asset is ever redrawn.
/// </summary>
public class FarmhandPaintMaskTests
{
    [Fact]
    public void Paint_mask_matches_the_farmhand_sheet_dimensions()
    {
        var assets = Path.Combine(FindWorkspaceRoot(), "Dayswork", "assets");

        var sheet = ReadPngSize(Path.Combine(assets, "farmhand.png"));
        var mask = ReadPngSize(Path.Combine(assets, "farmhand_PaintMask.png"));

        Assert.Equal(sheet, mask);
    }

    /// <summary>Reads width/height out of a PNG's IHDR chunk — the header is fixed-layout, so this
    /// needs no image decoder.</summary>
    private static (int Width, int Height) ReadPngSize(string path)
    {
        var header = new byte[24];
        using (var stream = File.OpenRead(path))
            Assert.Equal(header.Length, stream.Read(header, 0, header.Length));

        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, header[..4]);

        return (
            BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(16, 4)),
            BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(20, 4)));
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
