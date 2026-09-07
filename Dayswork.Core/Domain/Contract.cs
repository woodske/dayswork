namespace Dayswork.Core.Domain;

using Dayswork.Core.Crops;
using Dayswork.Core.FishPonds;
using Dayswork.Core.Machines;

/// <summary>
/// One farmhand contract. Since 2.0 a contract belongs to exactly one office building
/// (<paramref name="OfficeId"/> = that <c>Building.id</c>) and is sponsored by one player
/// (<paramref name="OwnerId"/> = that <c>Building.owner</c>, i.e. a
/// <c>Farmer.UniqueMultiplayerID</c>). The pair is the contract's identity in the world;
/// <paramref name="Id"/> stays the stable identity across edits.
/// </summary>
/// <param name="Revision">Edit counter, bumped on every committed mutation. Phase 4 uses it to
/// reject a client commit built against a contract the host has since changed.</param>
public sealed record Contract(
    ContractId Id,
    long OwnerId,
    Guid OfficeId,
    IReadOnlySet<TaskKind> EnabledTasks,
    IReadOnlyDictionary<TaskKind, DestinationKey> TaskDestinations,
    ContractSchedule Schedule,
    ContractStatus Status,
    GameDate HireDate,
    ContractScopeSelection ScopeSelection,
    ContractTermsSnapshot TermsSnapshot,
    EnergyTier Tier,
    IReadOnlyList<TaskCategory> CategoryPriority,
    CropPlan CropPlan,
    MachineWorkScope MachineScope,
    FishPondWorkScope FishPondScope,
    ContractPreferences Preferences,
    int Revision = 0
);
