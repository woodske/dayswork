using Dayswork.Integration;
using Xunit;

namespace Dayswork.Tests.Integration;

/// <summary>
/// Guards the three things that make Robin's paint menu work on the office, each of which fails
/// silently rather than loudly: the mask's size and colours (see <see cref="PaintMaskAsset"/>), and
/// the <c>Data/PaintData</c> value, which vanilla parses leniently enough that a malformed one
/// yields a menu with the wrong regions instead of an error.
/// </summary>
public sealed class OfficePaintMaskTests
{
    private static readonly (byte, byte, byte, byte) Transparent = (0, 0, 0, 0);
    private static readonly (byte, byte, byte, byte) Region1 = (255, 0, 0, 255);   // Color.Red
    private static readonly (byte, byte, byte, byte) Region2 = (0, 255, 0, 255);   // Color.Lime
    private static readonly (byte, byte, byte, byte) Region3 = (0, 0, 255, 255);   // Color.Blue

    [Fact]
    public void Paint_mask_matches_the_office_sheet_dimensions()
    {
        // BuildingPainter maps mask pixels onto the sheet by flat pixel index, so a mask that is not
        // exactly the sheet's size paints the wrong pixels, silently.
        var assets = Assets();

        Assert.Equal(
            PaintMaskAsset.ReadSize(Path.Combine(assets, "farmhand_office.png")),
            PaintMaskAsset.ReadSize(Path.Combine(assets, "farmhand_office_PaintMask.png")));
    }

    [Fact]
    public void Paint_mask_uses_only_the_three_exact_region_colours()
    {
        // The painter matches Color.Red / Lime / Blue by exact equality; anti-aliasing or an
        // off-by-one channel from a re-export marks nothing at all, again with no error.
        var colors = PaintMaskAsset.DistinctColors(Path.Combine(Assets(), "farmhand_office_PaintMask.png"));

        Assert.Empty(colors.Except(new[] { Transparent, Region1, Region2, Region3 }));
        Assert.Contains(Region1, colors);
        Assert.Contains(Region2, colors);
        Assert.Contains(Region3, colors);
    }

    [Fact]
    public void Paint_mask_asset_is_the_name_vanilla_derives_from_the_building_texture()
    {
        // Building.resetTexture() asks for textureName() + "_PaintMask" and nothing else; if the two
        // names drift apart the load fails, and BuildingPainter caches that failure for the session.
        Assert.Equal(HiringBuilding.TextureAsset + "_PaintMask", HiringBuilding.PaintMaskAsset);
    }

    [Fact]
    public void Paint_data_declares_three_regions_named_after_vanillas_translated_labels()
    {
        // BuildingPaintMenu.LoadRegionData splits on '/' and consumes pairs of
        // <RegionName>/<min> <max>, labelling each from Strings/Buildings:Paint_Region_<name> and
        // falling back to the raw name when there is no such string. Reusing vanilla's own region
        // names is what gets the office translated labels in all 13 languages for free.
        var parts = HiringBuilding.PaintData.Split('/');
        Assert.Equal(6, parts.Length);

        Assert.Equal(new[] { "Building", "Roof", "Trim" }, new[] { parts[0], parts[2], parts[4] });

        foreach (var range in new[] { parts[1], parts[3], parts[5] })
        {
            var bounds = range.Split(' ');
            Assert.Equal(2, bounds.Length);
            Assert.True(int.Parse(bounds[0]) < int.Parse(bounds[1]), $"'{range}' is not a min/max pair.");
        }
    }

    private static string Assets()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Dayswork.sln")))
                return Path.Combine(directory.FullName, "Dayswork", "assets");

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not find the Dayswork workspace root.");
    }
}
