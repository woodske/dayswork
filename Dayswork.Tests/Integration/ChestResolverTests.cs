using Dayswork.Integration;
using Microsoft.Xna.Framework;
using Xunit;

namespace Dayswork.Tests.Integration;

public sealed class ChestResolverTests
{
    // ReservedOfficeChestTiles walks the live farm, so the collection itself is exercised in-game.
    // What is worth pinning here is the shape the picker relies on: an office contributes exactly
    // its two porch tiles, at the documented offsets from the building's origin.
    [Fact]
    public void Office_porch_chest_tiles_are_two_distinct_offsets_from_the_building_origin()
    {
        Assert.NotEqual(HiringBuilding.InputChestDisplayTile, HiringBuilding.OutputChestDisplayTile);

        Assert.True(HiringBuilding.IsInputChestDisplayTile(
            HiringBuilding.InputChestDisplayTile.X, HiringBuilding.InputChestDisplayTile.Y));
        Assert.True(HiringBuilding.IsOutputChestDisplayTile(
            HiringBuilding.OutputChestDisplayTile.X, HiringBuilding.OutputChestDisplayTile.Y));

        Assert.False(HiringBuilding.IsInputChestDisplayTile(
            HiringBuilding.OutputChestDisplayTile.X, HiringBuilding.OutputChestDisplayTile.Y));
        Assert.False(HiringBuilding.IsOutputChestDisplayTile(
            HiringBuilding.InputChestDisplayTile.X, HiringBuilding.InputChestDisplayTile.Y));
    }

    [Fact]
    public void Porch_chest_tiles_sit_inside_the_building_footprint()
    {
        foreach (var displayTile in new[] { HiringBuilding.InputChestDisplayTile, HiringBuilding.OutputChestDisplayTile })
        {
            Assert.InRange(displayTile.X, 0, HiringBuilding.TilesWide - 1);
            Assert.InRange(displayTile.Y, 0, HiringBuilding.TilesHigh - 1);
        }
    }
}
