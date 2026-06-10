using Dayswork.Core.Compat;
using Dayswork.Core.Domain;
using FsCheck;

namespace Dayswork.Tests.Compat;

public static class ExpansionRouteGenerators
{
    private static readonly string[] LocationNames =
    {
        "Farm",
        SveExpansionProfile.GrandpasShedLocation,
        SveExpansionProfile.GrandpasShedGreenhouseLocation,
        "Greenhouse",
        "Barn",
    };

    private static readonly ExpansionRoutePurpose[] Purposes = Enum.GetValues<ExpansionRoutePurpose>();
    private static readonly ExpansionLocationRole[] Roles = Enum.GetValues<ExpansionLocationRole>();
    private static readonly ExpansionRouteFailureReason[] FailureReasons = Enum.GetValues<ExpansionRouteFailureReason>();

    public static Arbitrary<ExpansionRouteRequest> RouteRequests()
    {
        var gen =
            from width in Gen.Choose(1, 220)
            from height in Gen.Choose(1, 220)
            from source in Gen.Elements(LocationNames)
            from target in Gen.Elements(LocationNames)
            from purpose in Gen.Elements(Purposes)
            select new ExpansionRouteRequest(new FarmMapSignature(width, height), source, target, purpose);

        return gen.ToArbitrary();
    }

    public static Arbitrary<ExpansionRouteDefinition> RouteDefinitions()
    {
        var gen =
            from farm in Gen.Elements(
                new FarmMapSignature(140, 93),
                new FarmMapSignature(163, 156),
                new FarmMapSignature(156, 65))
            from purpose in Gen.Elements(Purposes)
            from hopCount in Gen.Choose(1, 5)
            from startIndex in Gen.Choose(0, LocationNames.Length - 1)
            from hops in Gen.Sequence(
                Enumerable.Range(0, hopCount)
                    .Select(i => RouteHop(i + 1, LocationNames[(startIndex + i) % LocationNames.Length], LocationNames[(startIndex + i + 1) % LocationNames.Length])))
            let hopList = hops.ToList()
            select new ExpansionRouteDefinition(
                new ExpansionRouteId($"generated.{farm.Width}.{farm.Height}.{purpose}.{hopCount}.{startIndex}"),
                farm,
                hopList[0].SourceLocationName,
                hopList[^1].TargetLocationName,
                purpose,
                hopList);

        return gen.ToArbitrary();
    }

    public static Arbitrary<ExpansionLocationDescriptor> LocationDescriptors()
    {
        var gen =
            from location in Gen.Elements(LocationNames.Where(name => name != "Farm").ToArray())
            from role in Gen.Elements(Roles)
            from associated in Gen.Elements(
                SveExpansionProfile.GrandpasShedGreenhouseLocation,
                "Greenhouse")
            select new ExpansionLocationDescriptor(location, location, role, associated);

        return gen.ToArbitrary();
    }

    public static Arbitrary<ExpansionRouteFailure> RouteFailures()
    {
        var gen =
            from request in RouteRequests().Generator
            from hasRouteId in Arb.Generate<bool>()
            from reason in Gen.Elements(FailureReasons)
            from hopOrdinal in Gen.Choose(1, 5)
            select new ExpansionRouteFailure(
                request,
                hasRouteId ? new ExpansionRouteId($"generated.{reason}") : null,
                hopOrdinal,
                reason,
                $"detail-{reason}");

        return gen.ToArbitrary();
    }

    private static Gen<ExpansionRouteHop> RouteHop(int ordinal, string source, string target)
    {
        return
            from approachX in Gen.Choose(0, 180)
            from approachY in Gen.Choose(0, 180)
            from arrivalX in Gen.Choose(0, 180)
            from arrivalY in Gen.Choose(0, 180)
            select new ExpansionRouteHop(
                ordinal,
                source,
                new TileCoord(approachX, approachY),
                target,
                new TileCoord(arrivalX, arrivalY));
    }
}
