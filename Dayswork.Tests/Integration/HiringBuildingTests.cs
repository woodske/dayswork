using Dayswork.Integration;
using Xunit;

namespace Dayswork.Tests.Integration;

public sealed class HiringBuildingTests
{
    [Fact]
    public void BuildData_places_no_cap_on_how_many_offices_the_farm_may_have()
    {
        // 2.0 allows any number of offices, one farmhand each. A leftover BuildCondition would
        // silently cap the farm at one and there would be nothing in the UI explaining why. Asserted
        // against the source because BuildData() reads i18n, which needs a live SMAPI helper.
        var source = File.ReadAllText(Path.Combine(
            FindWorkspaceRoot(),
            "Dayswork",
            "Integration",
            "HiringBuilding.cs"));

        Assert.DoesNotContain("BuildCondition =", source);
        Assert.DoesNotContain("BUILDINGS_CONSTRUCTED", source);
    }

    [Fact]
    public void BuiltInChests_declare_input_and_output_at_distinct_porch_tiles()
    {
        var source = File.ReadAllText(Path.Combine(
            FindWorkspaceRoot(),
            "Dayswork",
            "Integration",
            "HiringBuilding.cs"));
        Assert.Equal("Bindicle.Dayswork_Input", HiringBuilding.InputChestId);
        Assert.Equal("Bindicle.Dayswork_Output", HiringBuilding.OutputChestId);
        Assert.Contains("InputChestDisplayTile = new(1, 2)", source);
        Assert.Contains("OutputChestDisplayTile = new(3, 2)", source);
        Assert.Contains("Id = InputChestId", source);
        Assert.Contains("Id = OutputChestId", source);
        Assert.Contains("DisplayTile = InputChestDisplayTile.ToVector2()", source);
        Assert.Contains("DisplayTile = OutputChestDisplayTile.ToVector2()", source);
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
