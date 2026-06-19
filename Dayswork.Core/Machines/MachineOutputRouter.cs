namespace Dayswork.Core.Machines;

using Dayswork.Core.Domain;

/// <summary>
/// Builds the provenance → destination map for collected machine output, mirroring
/// <c>ManagedCropOutputRouter</c>. Each group's output is tagged with
/// <see cref="OutputScopeProvenance.Machine"/>(groupId) so the deposit planner routes it to the
/// group's chosen destination regardless of the buffer's nominal task tag.
/// </summary>
public static class MachineOutputRouter
{
    public static OutputScopeProvenance ProvenanceFor(MachineGroup group) =>
        OutputScopeProvenance.Machine(group.Id);

    public static IReadOnlyDictionary<OutputScopeProvenance, DestinationKey> BuildDestinationMap(
        IEnumerable<MachineGroup>? groups)
    {
        if (groups is null)
            return new Dictionary<OutputScopeProvenance, DestinationKey>();

        return groups
            .Where(group => group.OutputDestination is not AutomaticOutputDestination)
            .GroupBy(ProvenanceFor)
            .ToDictionary(
                group => group.Key,
                group => group.First().OutputDestination);
    }
}
