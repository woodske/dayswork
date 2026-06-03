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
