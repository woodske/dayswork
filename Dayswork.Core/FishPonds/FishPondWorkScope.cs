namespace Dayswork.Core.FishPonds;

using Dayswork.Core.Domain;

/// <summary>
/// The persisted, authored "Manage Fish Ponds" plan: a set of selected ponds plus one output
/// destination for everything they produce. Fish ponds are collect-only (the player stocks the fish
/// and supplies any capacity-quest item), so there is no input/reload configuration — this is much
/// simpler than <c>MachineWorkScope</c>. Carried directly on the contract.
/// </summary>
public sealed record FishPondWorkScope
{
    public static FishPondWorkScope Empty { get; } = new(Array.Empty<FishPondRef>(), AutomaticOutputDestination.Instance);

    public IReadOnlyList<FishPondRef> Ponds { get; }

    /// <summary>Where every collected pond stack is routed. Automatic ⇒ office output chest / shipping bin.</summary>
    public DestinationKey OutputDestination { get; }

    public FishPondWorkScope(IReadOnlyList<FishPondRef>? ponds, DestinationKey? outputDestination = null)
    {
        Ponds = (ponds ?? Array.Empty<FishPondRef>())
            .Where(pond => pond is not null && !string.IsNullOrWhiteSpace(pond.LocationName))
            .Distinct()
            .OrderBy(pond => pond.LocationName, StringComparer.Ordinal)
            .ThenBy(pond => pond.Tile.Y)
            .ThenBy(pond => pond.Tile.X)
            .ToList()
            .AsReadOnly();
        OutputDestination = outputDestination ?? AutomaticOutputDestination.Instance;
    }

    public bool IsEnabled => Ponds.Count > 0;

    public bool Equals(FishPondWorkScope? other) =>
        other is not null
        && Ponds.SequenceEqual(other.Ponds)
        && OutputDestination.Equals(other.OutputDestination);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var pond in Ponds)
            hash.Add(pond);
        hash.Add(OutputDestination);
        return hash.ToHashCode();
    }
}
