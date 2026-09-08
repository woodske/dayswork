using Dayswork.Core.Config;
using Dayswork.Core.Domain;
using Dayswork.Core.Net;
using Dayswork.Core.Persistence;
using Dayswork.Core.Pricing;
using Dayswork.Core.Upgrades;
using Dayswork.Integration;
using Dayswork.Orchestration;
using StardewValley;
using Season = Dayswork.Core.Domain.Season;

namespace Dayswork.Net;

/// <summary>
/// The host's side of the request protocol (2.0 plan D3). Every contract change in the game —
/// including the host's own, which arrives here as a direct "loopback" call rather than a message —
/// passes through these three methods, so single-player, host, split-screen guest, and remote
/// client all commit through one code path with one set of checks.
///
/// Requests are handled synchronously inside the message callback, so two clients' commits can
/// never interleave; a <see cref="RequestIdCache"/> makes a retried request idempotent, which is
/// what stops a one-time contract's price being charged twice after a client-side timeout.
/// </summary>
internal sealed class ContractRequestHandler
{
    private readonly OfficeContractStore _store;
    private readonly SaveDataSerializer _serializer;
    private readonly ContractTermsBuilder _termsBuilder;
    private readonly ModConfigManager _configManager;
    private readonly FarmhandUpgradeStore _upgradeStore;
    private readonly ContractReferenceResolver _references;
    private readonly ChestResolver _chestResolver;
    private readonly ShiftFleet _fleet;
    private readonly DaysworkSuspension _suspension;

    private readonly RequestIdCache _handled = new();

    public ContractRequestHandler(
        OfficeContractStore store,
        SaveDataSerializer serializer,
        ContractTermsBuilder termsBuilder,
        ModConfigManager configManager,
        FarmhandUpgradeStore upgradeStore,
        ContractReferenceResolver references,
        ChestResolver chestResolver,
        ShiftFleet fleet,
        DaysworkSuspension suspension)
    {
        _store = store;
        _serializer = serializer;
        _termsBuilder = termsBuilder;
        _configManager = configManager;
        _upgradeStore = upgradeStore;
        _references = references;
        _chestResolver = chestResolver;
        _fleet = fleet;
        _suspension = suspension;
    }

    /// <summary>Forgets remembered request ids — a new session's ids start clean.</summary>
    public void Reset() => _handled.Clear();

    // ── Commit ───────────────────────────────────────────────────────────────

    public ContractCommitResponseMessage HandleCommit(ContractCommitRequestMessage request, long senderId)
    {
        // A retry of something already done replays the original answer. Re-running it would charge
        // a one-time contract twice.
        if (!_handled.TryBegin(request.RequestId, out var previous)
            && previous is ContractCommitResponseMessage replay)
        {
            return replay;
        }

        var response = BuildCommitResponse(request, senderId);
        _handled.Remember(request.RequestId, response);
        return response;
    }

    private ContractCommitResponseMessage BuildCommitResponse(ContractCommitRequestMessage request, long senderId)
    {
        if (!Guid.TryParse(request.OfficeId, out var officeId))
            return Reject(request.RequestId, ContractRejectionCode.OfficeGone, "unparseable office id");

        var submitted = _serializer.DeserializeOne(request.ContractJson);
        if (submitted is null)
            return Reject(request.RequestId, ContractRejectionCode.VersionMismatch, "contract payload could not be read");

        var office = OfficeResolver.TryGet(officeId);
        var ownerId = office is null ? 0L : ResolveOwner(office);
        var stored = _store.ForOffice(officeId);
        var senderIsHost = senderId == Game1.MasterPlayer.UniqueMultiplayerID;

        // The terms are rebuilt here rather than trusted from the payload: pricing is the host's to
        // decide, and it is also what tells us whether the draft passes the confirm gate.
        var config = EffectiveConfigFor(ownerId);
        var preview = _termsBuilder.BuildPreview(
            submitted.ScopeSelection,
            submitted.EnabledTasks,
            submitted.Tier,
            config,
            submitted.CropPlan,
            submitted.MachineScope,
            submitted.FishPondScope);

        var missing = _references.Resolve(submitted);
        var upfrontPrice = !request.IsEdit && submitted.Schedule == ContractSchedule.OneTime
            ? preview.ProposedTerms?.Pricing.TotalPrice ?? 0
            : 0;

        var context = new CommitValidationContext(
            ProtocolMatches: request.ProtocolVersion == DaysworkProtocol.Version,
            Suspended: _suspension.IsSuspended,
            OfficeExists: office is not null,
            OfficeOwnerId: ownerId,
            SenderIsHost: senderIsHost,
            SenderId: senderId,
            StoredContract: stored,
            DraftIsValid: preview.IsValid && preview.ProposedTerms is not null,
            MissingChests: missing.MissingChests,
            MissingMachines: missing.MissingMachines,
            MissingPonds: missing.MissingPonds,
            OwnerMoney: office is null ? 0 : Sponsor.Money(ownerId),
            UpfrontPrice: upfrontPrice);

        if (ContractCommitValidator.Validate(request, context) is { } rejection)
            return Reject(request.RequestId, rejection, $"office {officeId:N}, sender {senderId}");

        var terms = preview.ProposedTerms!;
        if (upfrontPrice > 0)
            Sponsor.Charge(ownerId, upfrontPrice);

        Contract committed;
        if (request.IsEdit && stored is not null)
        {
            // An edit keeps the contract's identity, standing, and hire date; only the terms the
            // player just authored change.
            committed = _store.Update(officeId, submitted with
            {
                Id = stored.Id,
                OwnerId = ownerId,
                OfficeId = officeId,
                Status = stored.Status,
                HireDate = stored.HireDate,
                Revision = stored.Revision,
                TermsSnapshot = terms,
            });
        }
        else
        {
            committed = _store.Add(submitted with
            {
                OwnerId = ownerId,
                OfficeId = officeId,
                Status = ContractStatus.Active,
                HireDate = Today(),
                Revision = 0,
                TermsSnapshot = terms,
            });
        }

        return new ContractCommitResponseMessage
        {
            RequestId = request.RequestId,
            Accepted = true,
            Revision = committed.Revision,
        };
    }

    // ── Actions ──────────────────────────────────────────────────────────────

    public ContractActionResponseMessage HandleAction(ContractActionRequestMessage request, long senderId)
    {
        if (!_handled.TryBegin(request.RequestId, out var previous)
            && previous is ContractActionResponseMessage replay)
        {
            return replay;
        }

        var response = BuildActionResponse(request, senderId);
        _handled.Remember(request.RequestId, response);
        return response;
    }

    private ContractActionResponseMessage BuildActionResponse(ContractActionRequestMessage request, long senderId)
    {
        Guid.TryParse(request.OfficeId, out var officeId);
        var office = OfficeResolver.TryGet(officeId);
        var ownerId = office is null ? 0L : ResolveOwner(office);
        var stored = _store.ForOffice(officeId);

        var context = new ActionValidationContext(
            ProtocolMatches: request.ProtocolVersion == DaysworkProtocol.Version,
            Suspended: _suspension.IsSuspended,
            OfficeExists: office is not null,
            OfficeOwnerId: ownerId,
            SenderIsHost: senderId == Game1.MasterPlayer.UniqueMultiplayerID,
            SenderId: senderId,
            StoredContract: stored,
            ShiftRunning: stored is not null && _fleet.IsShiftRunning(stored.Id),
            OwnerIsResolvable: office is not null && Sponsor.Resolve(ownerId) is not null);

        if (ContractActionValidator.Validate(request, context) is { } rejection)
            return RejectAction(request, rejection, $"office {officeId:N}, sender {senderId}");

        switch (request.Action)
        {
            case ContractActionKind.Pause:
                _store.Pause(officeId);
                break;

            case ContractActionKind.Resume:
                _store.Resume(officeId);
                break;

            case ContractActionKind.Cancel:
                _store.Cancel(officeId);
                break;

            case ContractActionKind.ClaimOffice:
                // The owner's farmhand slot is gone, so the office is unusable as it stands: the
                // host takes it over and the orphaned contract is dropped, since its owner can
                // never be charged or paid again.
                office!.owner.Value = senderId;
                if (stored is { Status: ContractStatus.Active or ContractStatus.Paused })
                    _store.Cancel(officeId);
                break;

            case ContractActionKind.PurchaseUpgrade:
                return HandleUpgradePurchase(request, senderId);
        }

        return AcceptAction(request, senderId);
    }

    private ContractActionResponseMessage HandleUpgradePurchase(ContractActionRequestMessage request, long senderId)
    {
        if (!Enum.TryParse<FarmhandUpgradeKind>(request.UpgradeKind, ignoreCase: false, out var kind))
            return RejectAction(request, ContractRejectionCode.InvalidAction, $"unknown upgrade '{request.UpgradeKind}'");

        // Upgrades are bought by whoever asked, out of their own wallet, and belong to them alone.
        var state = _upgradeStore.For(senderId);
        var walletBefore = Sponsor.Money(senderId);
        var result = FarmhandUpgradePurchaser.TryPurchase(kind, state, walletBefore);
        switch (result.Status)
        {
            case FarmhandUpgradePurchaseStatus.Purchased:
                Sponsor.Charge(senderId, walletBefore - result.RemainingGold);
                _upgradeStore.Replace(senderId, result.State);
                if (kind == FarmhandUpgradeKind.Energy)
                    ApplyEnergyUpgradeToOpenContracts(senderId);
                return AcceptAction(request, senderId);

            case FarmhandUpgradePurchaseStatus.InsufficientFunds:
                return RejectAction(request, ContractRejectionCode.CannotAfford, kind.ToString());

            default:
                return RejectAction(request, ContractRejectionCode.InvalidAction, $"{kind} {result.Status}");
        }
    }

    /// <summary>The energy upgrade raises every tier's capacity, so contracts already written keep
    /// their stale terms unless refreshed. Only the buyer's own contracts change — an upgrade is
    /// theirs, not the farm's (2.0 plan D9).</summary>
    private void ApplyEnergyUpgradeToOpenContracts(long ownerId)
    {
        foreach (var contract in _store.OpenContracts().Where(c => c.OwnerId == ownerId).ToList())
        {
            _store.Update(
                contract.OfficeId,
                contract with { TermsSnapshot = FarmhandUpgradeEffects.AddEnergyBonus(contract.TermsSnapshot) });
        }
    }

    // ── Menu snapshot ────────────────────────────────────────────────────────

    /// <summary>
    /// What a client cannot see for itself: locations outside the farm and the chests in them.
    /// Stardew keeps a non-active location in sync only for the host, so a client's own scan of an
    /// expansion map or the greenhouse comes back empty and would silently offer nothing.
    /// </summary>
    public MenuSnapshotResponseMessage HandleSnapshotRequest(MenuSnapshotRequestMessage request, long senderId)
    {
        var upgrades = _upgradeStore.For(senderId);
        var response = new MenuSnapshotResponseMessage
        {
            RequestId = request.RequestId,
            ProtocolVersion = DaysworkProtocol.Version,
            OfficeId = request.OfficeId,
            SpeedPurchased = upgrades.SpeedPurchased,
            Speed2Purchased = upgrades.Speed2Purchased,
            EnergyPurchased = upgrades.EnergyPurchased,
        };

        if (ModEntry.ExpansionCompat is { } compat)
        {
            foreach (var descriptor in compat.GetExpansionLocationDescriptors())
            {
                if (!descriptor.IsWorkScopeEligible)
                    continue;

                response.ExpansionLocations.Add(new SnapshotLocation
                {
                    LocationName = descriptor.LocationName,
                    DisplayName = descriptor.DisplayName,
                    IsAvailable = compat.IsExpansionLocationAvailable(descriptor.LocationName),
                });
            }
        }

        foreach (var entry in _chestResolver.GetOffFarmChests())
        {
            response.OffFarmChests.Add(new SnapshotChest
            {
                LocationName = entry.LocationName,
                LocationDisplayName = entry.LocationDisplayName,
                TileX = entry.TileX,
                TileY = entry.TileY,
                DisplayName = entry.DisplayName,
                IsAutoGrabber = entry.IsAutoGrabber,
            });
        }

        return response;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>The config a contract runs under: the base snapshot plus the OWNER's upgrades.</summary>
    public ConfigSnapshot EffectiveConfigFor(long ownerId) =>
        FarmhandUpgradeEffects.Apply(_configManager.CurrentSnapshot, _upgradeStore.For(ownerId));

    /// <summary>Building.owner is set by buildStructure, so a carpenter-built office carries its
    /// builder's id; 0 means it predates ownership and is treated as the host's.</summary>
    public static long ResolveOwner(StardewValley.Buildings.Building office) =>
        office.owner.Value != 0 ? office.owner.Value : Game1.MasterPlayer.UniqueMultiplayerID;

    private static GameDate Today() =>
        new(Game1.Date.DayOfMonth, Enum.Parse<Season>(Game1.currentSeason, ignoreCase: true), Game1.year);

    private static ContractCommitResponseMessage Reject(string requestId, ContractRejectionCode code, string detail)
    {
        ModEntry.ModMonitor.Log($"[Dayswork] Rejected a contract commit ({code}): {detail}.", DevLog.WarnLevel);
        return new ContractCommitResponseMessage
        {
            RequestId = requestId,
            Accepted = false,
            Code = code,
            Detail = detail,
        };
    }

    private static ContractActionResponseMessage RejectAction(ContractActionRequestMessage request, ContractRejectionCode code, string detail)
    {
        ModEntry.ModMonitor.Log($"[Dayswork] Rejected a {request.Action} request ({code}): {detail}.", DevLog.WarnLevel);
        return new ContractActionResponseMessage
        {
            RequestId = request.RequestId,
            Action = request.Action,
            Accepted = false,
            Code = code,
            Detail = detail,
        };
    }

    private ContractActionResponseMessage AcceptAction(ContractActionRequestMessage request, long senderId)
    {
        var upgrades = _upgradeStore.For(senderId);
        return new ContractActionResponseMessage
        {
            RequestId = request.RequestId,
            Action = request.Action,
            Accepted = true,
            SpeedPurchased = upgrades.SpeedPurchased,
            Speed2Purchased = upgrades.Speed2Purchased,
            EnergyPurchased = upgrades.EnergyPurchased,
        };
    }
}
