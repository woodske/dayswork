namespace Dayswork.Core.Domain;

/// <summary>
/// Coarse work-priority groupings the player can reorder for a contract. The worker completes
/// all reachable work in the highest-priority category (nearest-first) before moving to the next.
/// Every <see cref="TaskKind"/> maps to exactly one category via <see cref="TaskKindSets.CategoryOf"/>.
/// </summary>
public enum TaskCategory
{
    AnimalCare,
    Crops,
    Fieldwork,
    Machines,
    FishPonds,
}
