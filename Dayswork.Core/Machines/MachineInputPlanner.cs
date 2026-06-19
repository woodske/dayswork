namespace Dayswork.Core.Machines;

/// <summary>
/// Pure supply-allocation for a machine reload pass — the analog of <c>ShiftSupplyAggregator</c> for
/// crops. Given the empty machines that could be loaded (each with its already-resolved candidate
/// recipes) and the input chest's contents, it greedily assigns the first satisfiable recipe to each
/// machine in order, clamping to available supply, and produces the exact per-id withdrawal totals.
///
/// Greedy-in-order means earlier candidates win scarce supply; later machines fall to
/// <see cref="MachineLoadPlan.Unsatisfied"/>. Atomic multi-item recipes (fish + coal) are only chosen
/// when every required item is fully available.
/// </summary>
public static class MachineInputPlanner
{
    public static MachineLoadPlan Plan(
        IReadOnlyList<MachineLoadCandidate> candidates,
        IReadOnlyDictionary<string, int> chestSupply)
    {
        if (candidates is null) throw new ArgumentNullException(nameof(candidates));
        if (chestSupply is null) throw new ArgumentNullException(nameof(chestSupply));

        var remaining = new Dictionary<string, int>(chestSupply, StringComparer.Ordinal);
        var assignments = new List<MachineLoadAssignment>();
        var withdrawals = new Dictionary<string, int>(StringComparer.Ordinal);
        var unsatisfied = new List<MachineRef>();

        foreach (var candidate in candidates)
        {
            var chosen = SelectSatisfiable(candidate.Options, remaining);
            if (chosen is null)
            {
                unsatisfied.Add(candidate.Machine);
                continue;
            }

            foreach (var (itemId, count) in chosen.TotalDemand())
            {
                remaining[itemId] = remaining.GetValueOrDefault(itemId) - count;
                withdrawals.TryGetValue(itemId, out var pulled);
                withdrawals[itemId] = pulled + count;
            }

            assignments.Add(new MachineLoadAssignment(candidate.Machine, chosen));
        }

        return new MachineLoadPlan(assignments.AsReadOnly(), withdrawals, unsatisfied.AsReadOnly());
    }

    private static RecipeRequirement? SelectSatisfiable(
        IReadOnlyList<RecipeRequirement> options,
        IReadOnlyDictionary<string, int> remaining)
    {
        foreach (var option in options)
        {
            if (option.TotalDemand().All(demand => remaining.GetValueOrDefault(demand.Key) >= demand.Value))
                return option;
        }

        return null;
    }
}
