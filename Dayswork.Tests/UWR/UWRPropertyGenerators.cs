using Dayswork.Core.Domain;
using Dayswork.Core.Shifts;
using FsCheck;

namespace Dayswork.Tests.UWR;

public static class UWRPropertyGenerators
{
    private static readonly TaskKind[] AllTasks = Enum.GetValues<TaskKind>();
    private static readonly TaskPriorityOrderer PriorityOrderer = new();

    public static Arbitrary<IReadOnlyList<WorkerRouteCandidate>> RouteCandidates()
    {
        var gen =
            from count in Gen.Choose(1, 40)
            from candidates in Gen.ArrayOf(count, RouteCandidate())
            select (IReadOnlyList<WorkerRouteCandidate>)candidates
                .Select((candidate, index) => candidate with
                {
                    CandidateId = index,
                    StableOrder = index,
                })
                .ToList();

        return gen.ToArbitrary();
    }

    public static Arbitrary<IReadOnlyList<WorkerRouteCandidate>> ReachableRouteCandidates()
    {
        var gen =
            from candidates in RouteCandidates().Generator
            select EnsureAtLeastOneReachable(candidates);

        return gen.ToArbitrary();
    }

    private static Gen<WorkerRouteCandidate> RouteCandidate()
    {
        return
            from task in Gen.Elements(AllTasks)
            from x in Gen.Choose(0, 120)
            from y in Gen.Choose(0, 120)
            from reachable in Arb.Generate<bool>()
            from routeCost in Gen.Choose(0, 500)
            select new WorkerRouteCandidate(
                CandidateId: 0,
                Task: task,
                PriorityRank: PriorityOrderer.Rank(task),
                StableOrder: 0,
                InteractionTile: new TileCoord(x, y),
                Reachable: reachable,
                RouteCost: routeCost);
    }

    private static IReadOnlyList<WorkerRouteCandidate> EnsureAtLeastOneReachable(
        IReadOnlyList<WorkerRouteCandidate> candidates)
    {
        if (candidates.Any(candidate => candidate.Reachable))
            return candidates;

        var first = candidates[0];
        return candidates
            .Select((candidate, index) => index == 0
                ? first with { Reachable = true, RouteCost = 0 }
                : candidate)
            .ToList();
    }
}
