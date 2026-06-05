using Dayswork.Integration;
using Xunit;

namespace Dayswork.Tests.Integration;

public sealed class HiringBuildingTests
{
    [Fact]
    public void BuildData_hides_office_when_one_already_exists_or_is_under_construction()
    {
        var source = File.ReadAllText(Path.Combine(
            FindWorkspaceRoot(),
            "Dayswork",
            "Integration",
            "HiringBuilding.cs"));

        Assert.Equal(
            "!BUILDINGS_CONSTRUCTED All Bindicle.Dayswork_Office 1 2147483647 true",
            HiringBuilding.OnePerFarmBuildCondition);
        Assert.EndsWith(" true", HiringBuilding.OnePerFarmBuildCondition);
        Assert.Contains(
            $"BuildCondition = {nameof(HiringBuilding.OnePerFarmBuildCondition)}",
            source);
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
