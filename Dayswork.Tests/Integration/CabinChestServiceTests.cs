using Dayswork.Integration;
using Xunit;

namespace Dayswork.Tests.Integration;

public sealed class CabinChestServiceTests
{
    [Fact]
    public void BuiltInDisplayTile_helpers_delegate_to_hiring_building_roles()
    {
        var source = File.ReadAllText(Path.Combine(
            FindWorkspaceRoot(),
            "Dayswork",
            "Integration",
            "CabinChestService.cs"));

        Assert.Contains("HiringBuilding.IsInputChestDisplayTile(localX, localY)", source);
        Assert.Contains("HiringBuilding.IsOutputChestDisplayTile(localX, localY)", source);
    }

    [Fact]
    public void Office_chests_are_upgraded_to_big_chests()
    {
        var source = File.ReadAllText(Path.Combine(
            FindWorkspaceRoot(),
            "Dayswork",
            "Integration",
            "CabinChestService.cs"));

        // Both porch chests get the vanilla Big Chest capacity (70 vs 36); the fallback-created
        // input chest is born big, and EnsureOfficeChests upgrades both idempotently.
        Assert.Contains("SpecialChestType = Chest.SpecialChestTypes.BigChest", source);
        Assert.Contains("ApplyBigChest(building.GetBuildingChest(HiringBuilding.InputChestId))", source);
        Assert.Contains("ApplyBigChest(building.GetBuildingChest(HiringBuilding.OutputChestId))", source);
    }

    [Fact]
    public void BuiltInChestNameKeys_are_distinct_and_role_specific()
    {
        Assert.Equal("building.office.input_chest.name", CabinChestService.InputChestNameKey);
        Assert.Equal("building.office.output_chest.name", CabinChestService.OutputChestNameKey);
        Assert.NotEqual(CabinChestService.InputChestNameKey, CabinChestService.OutputChestNameKey);
    }

    [Fact]
    public void LocalizedTextToken_uses_stardew_display_name_format()
    {
        Assert.Equal(
            "[LOCALIZED_TEXT building.office.input_chest.name]",
            CabinChestService.ToLocalizedTextToken(CabinChestService.InputChestNameKey));
    }

    private static string FindWorkspaceRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Dayswork.sln")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not find the Dayswork workspace root.");
    }
}
