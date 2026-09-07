using Dayswork.Core.Domain;

namespace Dayswork.Core.Shifts;

public enum WorkClaimDomain
{
    /// <summary>Generic tile work — per (location, tile, task), so two contracts doing
    /// different tasks on the same tile don't collide.</summary>
    TileTask,
    /// <summary>Animal care — per (animal id, task); animals move, so tiles are meaningless.</summary>
    Animal,
    /// <summary>Machine collect/reload — per (location, machine tile).</summary>
    Machine,
    /// <summary>Fish-pond collect — per (location, pond anchor tile).</summary>
    FishPond,
    /// <summary>Managed-crop lifecycle — per (location, dirt tile), independent of the generic
    /// TileTask domain so a generic water contract can still water a managed tile.</summary>
    ManagedDirt,
}

public readonly record struct WorkClaimKey(
    WorkClaimDomain Domain,
    string LocationName,
    int X,
    int Y,
    TaskKind? Task,
    long AnimalId)
{
    public static WorkClaimKey TileTask(string locationName, TileCoord tile, TaskKind task) =>
        new(WorkClaimDomain.TileTask, locationName, tile.X, tile.Y, task, 0);

    public static WorkClaimKey Animal(long animalId, TaskKind task) =>
        new(WorkClaimDomain.Animal, string.Empty, 0, 0, task, animalId);

    public static WorkClaimKey Machine(string locationName, TileCoord tile) =>
        new(WorkClaimDomain.Machine, locationName, tile.X, tile.Y, null, 0);

    public static WorkClaimKey FishPond(string locationName, TileCoord tile) =>
        new(WorkClaimDomain.FishPond, locationName, tile.X, tile.Y, null, 0);

    public static WorkClaimKey ManagedDirt(string locationName, TileCoord tile) =>
        new(WorkClaimDomain.ManagedDirt, locationName, tile.X, tile.Y, null, 0);
}

/// <summary>
/// Per-day registry that prevents two concurrent shifts from servicing the same work item when
/// their contracts' scopes overlap. Sessions claim work as they queue it (batch start / rescan);
/// the first contract to reach a batch wins its overlap. Claims are idempotent for their owner
/// (re-queues, replans, and deferred re-enqueues re-claim freely) and are never released — a
/// claimed-but-unworked item (e.g. the owner ran out of energy) simply waits until tomorrow.
/// Created fresh each morning; single-threaded by design (the fleet fans out sequentially).
/// </summary>
public sealed class WorkClaimRegistry
{
    private readonly Dictionary<WorkClaimKey, ContractId> _claims = new();

    /// <summary>True if the key is unclaimed (now claimed by <paramref name="owner"/>) or was
    /// already claimed by <paramref name="owner"/>; false when another contract holds it.</summary>
    public bool TryClaim(WorkClaimKey key, ContractId owner)
    {
        if (_claims.TryGetValue(key, out var existing))
            return existing == owner;

        _claims[key] = owner;
        return true;
    }

    public bool IsClaimedByOther(WorkClaimKey key, ContractId owner) =>
        _claims.TryGetValue(key, out var existing) && existing != owner;

    public int Count => _claims.Count;
}
