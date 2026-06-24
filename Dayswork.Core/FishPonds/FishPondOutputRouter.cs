namespace Dayswork.Core.FishPonds;

using Dayswork.Core.Domain;

/// <summary>
/// Builds the provenance → destination map for collected fish-pond output, mirroring
/// <c>MachineOutputRouter</c>. All ponds share one <see cref="OutputScopeProvenance.FishPond"/>
/// provenance, so the deposit planner routes every pond stack to the scope's single chosen
/// destination regardless of the buffer's nominal task tag.
///
/// Unlike <c>MachineOutputRouter</c> (which omits Automatic and lets the planner fall back to the
/// task assignment), this always maps the pond provenance — including the Automatic case, which
/// maps to <see cref="AutomaticOutputDestination"/> so a pond's output can never be mis-routed to an
/// unrelated per-task destination just because it shares the buffer's nominal task tag.
/// </summary>
public static class FishPondOutputRouter
{
    public static OutputScopeProvenance Provenance { get; } = OutputScopeProvenance.FishPond();

    public static IReadOnlyDictionary<OutputScopeProvenance, DestinationKey> BuildDestinationMap(
        FishPondWorkScope? scope)
    {
        var map = new Dictionary<OutputScopeProvenance, DestinationKey>();
        if (scope is { IsEnabled: true })
            map[Provenance] = scope.OutputDestination;

        return map;
    }
}
