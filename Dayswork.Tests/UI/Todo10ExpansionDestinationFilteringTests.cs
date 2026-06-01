using Dayswork.Compat;
using Dayswork.Core.Compat;
using Dayswork.Core.Domain;
using Xunit;

namespace Dayswork.Tests.UI;

public sealed class Todo10ExpansionDestinationFilteringTests
{
    private static ExpansionCompatService Service()
    {
        var service = new ExpansionCompatService(new VanillaExpansionProfile(), new AnimalBuildingCapacityPolicy());
        service.SetActiveProfile(new SveExpansionProfile());
        return service;
    }

    [Fact]
    public void Shed_chests_are_hidden_without_a_greenhouse_selection()
    {
        var service = Service();
        var shed = Assert.Single(
            service.GetExpansionLocationDescriptors(),
            descriptor => descriptor.LocationName == SveExpansionProfile.GrandpasShedLocation);

        Assert.False(service.IsExpansionChestVisibleForScope(shed, selectedGreenhouse: null));
    }

    [Fact]
    public void Shed_chests_are_hidden_for_standard_greenhouse_selection()
    {
        var service = Service();
        var descriptors = service.GetExpansionLocationDescriptors();

        foreach (var descriptor in descriptors)
        {
            Assert.False(service.IsExpansionChestVisibleForScope(
                descriptor,
                new GreenhouseSelection("Greenhouse")));
        }
    }

    [Fact]
    public void Shed_and_shed_greenhouse_chests_are_visible_for_shed_greenhouse_selection()
    {
        var service = Service();
        var selection = new GreenhouseSelection(SveExpansionProfile.GrandpasShedGreenhouseLocation);

        var visible = service.GetExpansionLocationDescriptors()
            .Where(descriptor => service.IsExpansionChestVisibleForScope(descriptor, selection))
            .Select(descriptor => descriptor.LocationName)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            new[]
            {
                SveExpansionProfile.GrandpasShedLocation,
                SveExpansionProfile.GrandpasShedGreenhouseLocation,
            }.OrderBy(name => name, StringComparer.Ordinal),
            visible);
    }

    [Fact]
    public void Main_shed_never_becomes_a_work_scope_descriptor()
    {
        var service = Service();
        var shed = Assert.Single(
            service.GetExpansionLocationDescriptors(),
            descriptor => descriptor.LocationName == SveExpansionProfile.GrandpasShedLocation);

        Assert.False(shed.IsWorkScopeEligible);
        Assert.Equal(ExpansionLocationRole.DepositOnly, shed.Role);
    }
}
