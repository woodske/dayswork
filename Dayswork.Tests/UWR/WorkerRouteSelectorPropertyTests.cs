using Dayswork.Core.Shifts;
using FsCheck.Xunit;
using Xunit;

namespace Dayswork.Tests.UWR;

public sealed class WorkerRouteSelectorPropertyTests
{
    private readonly WorkerRouteSelector _selector = new();

    [Property(Arbitrary = new[] { typeof(UWRPropertyGenerators) }, MaxTest = 500)]
    public void Selected_candidate_has_best_priority_then_minimum_cost(
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

        // Strict between categories: selected is in the best (lowest) priority rank present...
        var bestRank = reachable.Min(candidate => candidate.PriorityRank);
        Assert.Equal(bestRank, selected!.PriorityRank);

        // ...and nearest within that rank.
        var minCostInBestRank = reachable
            .Where(candidate => candidate.PriorityRank == bestRank)
            .Min(candidate => candidate.RouteCost);
        Assert.Equal(minCostInBestRank, selected.RouteCost);
    }

    [Property(Arbitrary = new[] { typeof(UWRPropertyGenerators) }, MaxTest = 500)]
    public void Selection_matches_priority_then_cost_tie_break_oracle(
        IReadOnlyList<WorkerRouteCandidate> candidates)
    {
        var expected = candidates
            .Where(candidate => candidate.Reachable)
            .OrderBy(candidate => candidate.PriorityRank)
            .ThenBy(candidate => candidate.RouteCost)
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
    public void Best_priority_zero_cost_candidate_wins(
        IReadOnlyList<WorkerRouteCandidate> candidates)
    {
        var reachable = candidates.Where(candidate => candidate.Reachable).ToList();
        if (reachable.Count == 0)
            return;

        // Highest possible priority (lowest rank) and zero cost: must win outright.
        var winner = reachable[0] with
        {
            CandidateId = 10_000,
            RouteCost = 0,
            PriorityRank = int.MinValue,
            StableOrder = int.MinValue,
        };
        var selected = _selector.Select(candidates.Append(winner));

        Assert.Equal(winner, selected);
    }
}
