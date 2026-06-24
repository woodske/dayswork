using Dayswork.Core.Domain;
using Dayswork.Core.FishPonds;

namespace Dayswork.UI;

/// <summary>
/// Transient, mutable in-progress fish-pond plan the Manage Fish Ponds page edits: the ponds selected
/// on the map plus one output destination for everything they produce. Projects into the persisted
/// <see cref="FishPondWorkScope"/>. Much simpler than <see cref="MachinePlanDraft"/> — fish ponds are
/// collect-only (no groups, no input filter/chest/mode), so this is a flat list + one destination.
/// </summary>
internal sealed class FishPondPlanDraft
{
    public List<FishPondRef> Ponds { get; } = new();
    public DestinationKey? OutputDestination { get; set; }

    public bool HasAnyAssignment => Ponds.Count > 0;

    public void SetPonds(IEnumerable<FishPondRef> ponds)
    {
        Ponds.Clear();
        Ponds.AddRange(ponds);
    }

    public FishPondWorkScope BuildScope() =>
        new(Ponds.ToList(), OutputDestination ?? AutomaticOutputDestination.Instance);

    public void HydrateFrom(FishPondWorkScope scope)
    {
        Ponds.Clear();
        OutputDestination = null;
        if (!scope.IsEnabled)
            return;

        Ponds.AddRange(scope.Ponds);
        OutputDestination = scope.OutputDestination;
    }
}
