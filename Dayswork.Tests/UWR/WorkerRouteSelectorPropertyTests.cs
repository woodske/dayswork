using Dayswork.Core.Shifts;
using FsCheck.Xunit;
using Xunit;

namespace Dayswork.Tests.UWR;

public sealed class WorkerRouteSelectorPropertyTests
{
    private readonly WorkerRouteSelector _selector = new();

    [Property(Arbitrary = new[] { typeof(UWRPropertyGenerators) }, MaxTest = 500)]
    public void Selected_candidate_has_minimum_reachable_route_cost(
        IReadOnlyList<WorkerRouteCandidate> candidates)
    {
        var selected = _selector.Select(candidates);
        var reachable = candidates.Where(candidate => candidate.Reachable).ToList();

        if (reachable.Count == 0)
        {
            Assert.Null(selected);
            return;
        }

        Assert.NotNull(selected);
        Assert.Equal(reachable.Min(candidate => candidate.RouteCost), selected!.RouteCost);
    }

    [Property(Arbitrary = new[] { typeof(UWRPropertyGenerators) }, MaxTest = 500)]
    public void Selection_matches_minimum_cost_tie_break_oracle(
        IReadOnlyList<WorkerRouteCandidate> candidates)
    {
        var expected = candidates
            .Where(candidate => candidate.Reachable)
            .OrderBy(candidate => candidate.RouteCost)
            .ThenBy(candidate => candidate.PriorityRank)
            .ThenBy(candidate => candidate.StableOrder)
            .FirstOrDefault();

        var actual = _selector.Select(candidates);

        Assert.Equal(expected, actual);
    }

    [Property(Arbitrary = new[] { typeof(UWRPropertyGenerators) }, MaxTest = 500)]
    public void Unreachable_candidates_are_never_selected_when_reachable_candidates_exist(
        IReadOnlyList<WorkerRouteCandidate> candidates)
    {
        var selected = _selector.Select(candidates);

        if (candidates.Any(candidate => candidate.Reachable))
        {
            Assert.NotNull(selected);
            Assert.True(selected!.Reachable);
        }
    }

    [Property(Arbitrary = new[] { typeof(UWRPropertyGenerators) }, MaxTest = 500)]
    public void Zero_cost_reachable_candidate_beats_positive_cost_candidates(
        IReadOnlyList<WorkerRouteCandidate> candidates)
    {
        var reachable = candidates.Where(candidate => candidate.Reachable).ToList();
        if (reachable.Count == 0)
            return;

        var zero = reachable[0] with
        {
            CandidateId = 10_000,
            RouteCost = 0,
            PriorityRank = int.MaxValue,
            StableOrder = int.MaxValue,
        };
        var positives = candidates.Select(candidate => candidate with
        {
            RouteCost = candidate.Reachable ? Math.Max(1, candidate.RouteCost) : candidate.RouteCost,
        });
        var selected = _selector.Select(positives.Append(zero));

        Assert.Equal(zero, selected);
    }
}
