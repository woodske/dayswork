using Dayswork.Compat;
using Dayswork.Core.Compat;
using Dayswork.Core.Domain;
using FsCheck.Xunit;
using Xunit;

namespace Dayswork.Tests.Compat;

public sealed class ExpansionRoutePropertyTests
{
    [Property(Arbitrary = new[] { typeof(ExpansionRouteGenerators) }, MaxTest = 500)]
    public void Sve_route_lookup_is_deterministic(ExpansionRouteRequest request)
    {
        var profile = new SveExpansionProfile();

        var firstResult = profile.TryGetRoute(request, out var first);
        var secondResult = profile.TryGetRoute(request, out var second);

        Assert.Equal(firstResult, secondResult);
        Assert.Equal(first, second);
    }

    [Property(Arbitrary = new[] { typeof(ExpansionRouteGenerators) }, MaxTest = 500)]
    public void Route_hop_ordinals_are_contiguous(ExpansionRouteDefinition route)
    {
        var expected = Enumerable.Range(1, route.Hops.Count).ToArray();
        var actual = route.Hops.Select(hop => hop.Ordinal).ToArray();

        Assert.Equal(expected, actual);
    }

    [Property(Arbitrary = new[] { typeof(ExpansionRouteGenerators) }, MaxTest = 500)]
    public void Route_hops_are_contiguous_by_location(ExpansionRouteDefinition route)
    {
        for (var i = 1; i < route.Hops.Count; i++)
        {
            Assert.Equal(route.Hops[i - 1].TargetLocationName, route.Hops[i].SourceLocationName);
        }
    }

    [Property(Arbitrary = new[] { typeof(ExpansionRouteGenerators) }, MaxTest = 500)]
    public void Expansion_chest_visibility_requires_associated_greenhouse_selection(
        ExpansionLocationDescriptor descriptor,
        bool selectAssociated)
    {
        var service = new ExpansionCompatService(new VanillaExpansionProfile(), new AnimalBuildingCapacityPolicy());
        var selected = selectAssociated
            ? new GreenhouseSelection(descriptor.AssociatedWorkLocationName)
            : new GreenhouseSelection($"{descriptor.AssociatedWorkLocationName}.Other");

        var visible = service.IsExpansionChestVisibleForScope(descriptor, selected);

        Assert.Equal(descriptor.IsDepositDestinationEligible && selectAssociated, visible);
    }

    [Property(Arbitrary = new[] { typeof(ExpansionRouteGenerators) }, MaxTest = 500)]
    public void Route_failure_log_payload_contains_reason_target_and_purpose(ExpansionRouteFailure failure)
    {
        var message = ExpansionCompatService.FormatRouteFailure(failure);

        Assert.Contains(failure.Reason.ToString(), message);
        Assert.Contains(failure.Request.TargetLocationName, message);
        Assert.Contains(failure.Request.Purpose.ToString(), message);
    }
}
