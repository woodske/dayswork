namespace Dayswork.Core.Machines;

/// <summary>
/// A secondary item a machine consumes alongside its primary input (e.g. the fish smoker's coal),
/// modeled either as <c>Data/Machines</c> trigger requirements or machine-level
/// <c>AdditionalConsumedItems</c>. Resolved against live game data in <c>Dayswork/</c> and snapshotted
/// here so the Core planner stays SMAPI-free.
/// </summary>
public sealed record AdditionalInput(string QualifiedId, int RequiredCount)
{
    public int RequiredCount { get; } = RequiredCount < 1 ? 1 : RequiredCount;
}

/// <summary>
/// One concrete way to load a machine: consume <see cref="RequiredCount"/> of
/// <see cref="InputQualifiedId"/> plus all <see cref="AdditionalConsumed"/> items. A snapshot of the
/// already-resolved recipe match (context tags / <c>MachineDataUtility</c> checks happen in
/// <c>Dayswork/</c>), so the planner only does pure supply allocation.
/// </summary>
public sealed record RecipeRequirement
{
    public string InputQualifiedId { get; }
    public int RequiredCount { get; }
    public IReadOnlyList<AdditionalInput> AdditionalConsumed { get; }

    public RecipeRequirement(
        string inputQualifiedId,
        int requiredCount = 1,
        IReadOnlyList<AdditionalInput>? additionalConsumed = null)
    {
        InputQualifiedId = string.IsNullOrWhiteSpace(inputQualifiedId)
            ? throw new ArgumentException("Recipe input id must be set.", nameof(inputQualifiedId))
            : inputQualifiedId;
        RequiredCount = requiredCount < 1 ? 1 : requiredCount;
        AdditionalConsumed = (additionalConsumed ?? Array.Empty<AdditionalInput>())
            .Where(item => item is not null && !string.IsNullOrWhiteSpace(item.QualifiedId))
            .ToList()
            .AsReadOnly();
    }

    /// <summary>Total demand per qualified id for this recipe (primary input folded in with additionals).</summary>
    public IReadOnlyDictionary<string, int> TotalDemand()
    {
        var demand = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [InputQualifiedId] = RequiredCount,
        };

        foreach (var additional in AdditionalConsumed)
        {
            demand.TryGetValue(additional.QualifiedId, out var existing);
            demand[additional.QualifiedId] = existing + additional.RequiredCount;
        }

        return demand;
    }

    public bool Equals(RecipeRequirement? other) =>
        other is not null
        && string.Equals(InputQualifiedId, other.InputQualifiedId, StringComparison.Ordinal)
        && RequiredCount == other.RequiredCount
        && AdditionalConsumed.SequenceEqual(other.AdditionalConsumed);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(InputQualifiedId, StringComparer.Ordinal);
        hash.Add(RequiredCount);
        foreach (var additional in AdditionalConsumed)
            hash.Add(additional);
        return hash.ToHashCode();
    }
}

/// <summary>
/// An empty machine and the ordered set of recipes that would load it given the group's input filter.
/// Options are tried in order; the first one the remaining supply can fully satisfy is chosen.
/// </summary>
public sealed record MachineLoadCandidate(MachineRef Machine, IReadOnlyList<RecipeRequirement> Options);

/// <summary>A machine the planner committed to loading, with the chosen recipe.</summary>
public sealed record MachineLoadAssignment(MachineRef Machine, RecipeRequirement Requirement);
