using Dayswork.Core.Compat;
using Dayswork.Core.Domain;
using Xunit;

namespace Dayswork.Tests.Compat;

public sealed class Todo10RouteDefinitionTests
{
    [Theory]
    [InlineData(140, 93, 20, 22)]
    [InlineData(163, 156, 144, 34)]
    [InlineData(156, 65, 18, 22)]
    public void Sve_work_entry_routes_farm_to_greenhouse_through_main_shed(
        int width,
        int height,
        int farmApproachX,
        int farmApproachY)
    {
        var profile = new SveExpansionProfile();
        var request = new ExpansionRouteRequest(
            new FarmMapSignature(width, height),
            "Farm",
            SveExpansionProfile.GrandpasShedGreenhouseLocation,
            ExpansionRoutePurpose.WorkEntry);

        Assert.True(profile.TryGetRoute(request, out var route));
        Assert.Equal(2, route.Hops.Count);

        var farmToShed = route.Hops[0];
        Assert.Equal("Farm", farmToShed.SourceLocationName);
        Assert.Equal(SveExpansionProfile.GrandpasShedLocation, farmToShed.TargetLocationName);
        Assert.Equal(new TileCoord(farmApproachX, farmApproachY), farmToShed.ApproachTile);
        Assert.Equal(new TileCoord(15, 16), farmToShed.ArrivalTile);

        var shedToGreenhouse = route.Hops[1];
        Assert.Equal(SveExpansionProfile.GrandpasShedLocation, shedToGreenhouse.SourceLocationName);
        Assert.Equal(SveExpansionProfile.GrandpasShedGreenhouseLocation, shedToGreenhouse.TargetLocationName);
        Assert.Equal(new TileCoord(25, 14), shedToGreenhouse.ApproachTile);
        Assert.Equal(new TileCoord(30, 16), shedToGreenhouse.ArrivalTile);
    }

    [Fact]
    public void Sve_has_no_direct_work_entry_shortcut_from_farm_to_shed_greenhouse()
    {
        var profile = new SveExpansionProfile();
        var request = new ExpansionRouteRequest(
            new FarmMapSignature(140, 93),
            "Farm",
            SveExpansionProfile.GrandpasShedGreenhouseLocation,
            ExpansionRoutePurpose.WorkEntry);

        Assert.True(profile.TryGetRoute(request, out var route));
        Assert.DoesNotContain(route.Hops, hop =>
            hop.SourceLocationName == "Farm" &&
            hop.TargetLocationName == SveExpansionProfile.GrandpasShedGreenhouseLocation);
    }

    [Fact]
    public void Sve_unsupported_farm_signature_has_no_route()
    {
        var profile = new SveExpansionProfile();
        var request = new ExpansionRouteRequest(
            new FarmMapSignature(80, 65),
            "Farm",
            SveExpansionProfile.GrandpasShedGreenhouseLocation,
            ExpansionRoutePurpose.WorkEntry);

        Assert.False(profile.TryGetRoute(request, out _));
    }

    [Fact]
    public void Sve_descriptors_separate_work_scope_from_deposit_only_shed()
    {
        var descriptors = new SveExpansionProfile().GetLocationDescriptors();

        var greenhouse = Assert.Single(
            descriptors,
            descriptor => descriptor.LocationName == SveExpansionProfile.GrandpasShedGreenhouseLocation);
        Assert.Equal(ExpansionLocationRole.GreenhouseWork, greenhouse.Role);
        Assert.True(greenhouse.IsWorkScopeEligible);
        Assert.True(greenhouse.IsDepositDestinationEligible);
        Assert.Equal(SveExpansionProfile.GrandpasShedGreenhouseLocation, greenhouse.AssociatedWorkLocationName);

        var shed = Assert.Single(
            descriptors,
            descriptor => descriptor.LocationName == SveExpansionProfile.GrandpasShedLocation);
        Assert.Equal(ExpansionLocationRole.DepositOnly, shed.Role);
        Assert.False(shed.IsWorkScopeEligible);
        Assert.True(shed.IsDepositDestinationEligible);
        Assert.Equal(SveExpansionProfile.GrandpasShedGreenhouseLocation, shed.AssociatedWorkLocationName);
    }

    [Theory]
    [InlineData(140, 93, 20, 22)]
    [InlineData(163, 156, 144, 34)]
    [InlineData(156, 65, 18, 22)]
    public void Sve_return_routes_go_greenhouse_to_shed_to_farm(
        int width,
        int height,
        int farmArrivalX,
        int farmArrivalY)
    {
        var profile = new SveExpansionProfile();
        var request = new ExpansionRouteRequest(
            new FarmMapSignature(width, height),
            SveExpansionProfile.GrandpasShedGreenhouseLocation,
            "Farm",
            ExpansionRoutePurpose.ReturnToFarm);

        Assert.True(profile.TryGetRoute(request, out var route));
        Assert.Equal(2, route.Hops.Count);
        Assert.Equal(new TileCoord(30, 16), route.Hops[0].ApproachTile);
        Assert.Equal(new TileCoord(25, 14), route.Hops[0].ArrivalTile);
        Assert.Equal(new TileCoord(15, 17), route.Hops[1].ApproachTile);
        Assert.Equal(new TileCoord(farmArrivalX, farmArrivalY), route.Hops[1].ArrivalTile);
    }
}
