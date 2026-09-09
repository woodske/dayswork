using Dayswork.Core.Domain;
using Dayswork.Core.Net;
using Dayswork.Core.Persistence;
using Dayswork.Guards;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace Dayswork.Net;

/// <summary>
/// The asking side of the request protocol. Every caller — the host's own menus included — goes
/// through here, so there is one commit path in the codebase rather than a local one and a network
/// one that can drift.
///
/// When the host is in this process (single-player, the host's own screen, a split-screen guest)
/// the request is handed straight to <see cref="ContractRequestHandler"/> and answered
/// synchronously. Only a genuinely remote client puts anything on the wire, and only that path has
/// a timeout to worry about.
/// </summary>
internal sealed class ContractRequestClient
{
    /// <summary>How long a remote client waits before telling the player the host did not answer.
    /// Wall clock, not game time: the host may be in a menu of its own.</summary>
    private static readonly TimeSpan ResponseTimeout = TimeSpan.FromSeconds(10);

    internal sealed record Pending(
        string RequestId,
        DateTime SentAt,
        Action<ContractCommitResponseMessage>? OnCommit,
        Action<ContractActionResponseMessage>? OnAction);

    private readonly NetChannel _channel;
    private readonly ContractRequestHandler _handler;
    private readonly SaveDataSerializer _serializer;
    private readonly string _modVersion;
    private readonly DaysworkSuspension _suspension;
    private readonly OfficeContractStore _store;

    // Per screen: two local players can each have a menu and outstanding request. This is keyed
    // explicitly instead of using PerScreen<T> because lifecycle cleanup must remove exactly the
    // screen whose event fired without touching another screen's callbacks.
    private readonly Dictionary<int, Dictionary<string, Pending>> _pendingByScreen = new();

    public ContractRequestClient(
        NetChannel channel,
        ContractRequestHandler handler,
        SaveDataSerializer serializer,
        string modVersion,
        DaysworkSuspension suspension,
        OfficeContractStore store)
    {
        _channel = channel;
        _handler = handler;
        _serializer = serializer;
        _modVersion = modVersion;
        _suspension = suspension;
        _store = store;
    }

    /// <summary>Raised when a request times out with no answer, so the UI can say so and keep the
    /// player's draft.</summary>
    public Action? TimedOut { get; set; }

    // ── Sending ──────────────────────────────────────────────────────────────

    public void SubmitContract(
        Guid officeId,
        Contract contract,
        bool isEdit,
        int expectedRevision,
        Action<ContractCommitResponseMessage> onResult)
    {
        if (Authority.IsRemoteClient && !CanDispatchToHost())
        {
            onResult(new ContractCommitResponseMessage
            {
                Accepted = false,
                Code = HostUnavailableCode(),
            });
            return;
        }

        var request = new ContractCommitRequestMessage
        {
            RequestId = NewRequestId(),
            ProtocolVersion = DaysworkProtocol.Version,
            OfficeId = officeId.ToString("N"),
            ContractJson = _serializer.SerializeOne(contract, _modVersion),
            IsEdit = isEdit,
            ExpectedRevision = expectedRevision,
        };

        if (Authority.HostIsInThisProcess)
        {
            onResult(_handler.HandleCommit(request, Game1.player.UniqueMultiplayerID));
            return;
        }

        Track(new Pending(request.RequestId, DateTime.UtcNow, onResult, null));
        _channel.SendToHost(DaysworkProtocol.ContractCommitRequest, request);
    }

    public void SubmitAction(
        Guid officeId,
        ContractActionKind action,
        Action<ContractActionResponseMessage> onResult,
        string upgradeKind = "")
    {
        if (Authority.IsRemoteClient && !CanDispatchToHost())
        {
            onResult(new ContractActionResponseMessage
            {
                Action = action,
                Accepted = false,
                Code = HostUnavailableCode(),
            });
            return;
        }

        var request = new ContractActionRequestMessage
        {
            RequestId = NewRequestId(),
            ProtocolVersion = DaysworkProtocol.Version,
            OfficeId = officeId.ToString("N"),
            Action = action,
            UpgradeKind = upgradeKind,
        };

        if (Authority.HostIsInThisProcess)
        {
            onResult(_handler.HandleAction(request, Game1.player.UniqueMultiplayerID));
            return;
        }

        Track(new Pending(request.RequestId, DateTime.UtcNow, null, onResult));
        _channel.SendToHost(DaysworkProtocol.ContractActionRequest, request);
    }

    /// <summary>
    /// Asks the host for the parts of its world this client cannot see. Only a remote client needs
    /// this: on the host's own computer the menus read the world directly.
    /// </summary>
    public void RequestMenuSnapshot(Guid officeId)
    {
        if (!Authority.IsRemoteClient || !CanDispatchToHost())
            return;

        _channel.SendToHost(DaysworkProtocol.MenuSnapshotRequest, new MenuSnapshotRequestMessage
        {
            RequestId = NewRequestId(),
            ProtocolVersion = DaysworkProtocol.Version,
            OfficeId = officeId.ToString("N"),
        });
    }

    // ── Receiving ────────────────────────────────────────────────────────────

    public void ReceiveCommitResponse(ContractCommitResponseMessage response)
    {
        if (Take(response.RequestId) is not { } pending)
            return;

        ApplyAuthoritativeState(response.State);
        pending.OnCommit?.Invoke(response);
    }

    public void ReceiveActionResponse(ContractActionResponseMessage response)
    {
        if (Take(response.RequestId) is not { } pending)
            return;

        ApplyAuthoritativeState(response.State);
        pending.OnAction?.Invoke(response);
    }

    /// <summary>
    /// Folds the host's word on an office into this client's read cache, before the callback that
    /// redraws the menu runs (R7). Without this the menu redraws from the copy the client loaded
    /// with: an accepted Pause still reads Active, and clicking again submits the same Pause.
    /// <para>
    /// Only a remote client does this. On the host's own computer — single-player included — the
    /// store <em>is</em> the authority and was already mutated by the handler; hydrating it back
    /// from a serialized copy would be a pointless round trip through JSON.
    /// </para>
    /// </summary>
    internal void ApplyAuthoritativeState(AuthoritativeContractState? state)
    {
        if (Authority.HostIsInThisProcess || !AuthoritativeContractCache.TryAccept(state))
            return;

        if (!Guid.TryParse(state!.OfficeId, out var officeId))
            return;

        AuthoritativeContractCache.RememberShiftRunning(officeId, state.ShiftRunning);

        // A demolished office keeps no contract to show; the building itself is gone from the
        // client's synced world too, so the menu closes on its own next refresh.
        var contract = state is { OfficeExists: true, HasContract: true }
            ? _serializer.DeserializeOne(state.ContractJson)
            : null;

        _store.HydrateOffice(officeId, contract);
    }

    /// <summary>
    /// Drops requests the host never answered. The host may in fact have committed — the next time
    /// the hub opens it reads the office's modData and shows the truth — so the player is told that
    /// their changes were not confirmed rather than that they were lost.
    /// </summary>
    public void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
    {
        var pending = PendingForScreen(Context.ScreenId);
        if (pending.Count == 0 || !e.IsMultipleOf(60))
            return;

        var now = DateTime.UtcNow;
        var expired = pending.Values.Where(entry => now - entry.SentAt > ResponseTimeout).ToList();
        foreach (var entry in expired)
        {
            pending.Remove(entry.RequestId);
            ModEntry.ModMonitor.Log(
                $"[Dayswork] The host did not answer request {entry.RequestId} within {ResponseTimeout.TotalSeconds:0}s.",
                DevLog.WarnLevel);
            TimedOut?.Invoke();
        }
    }

    /// <summary>Clears only the current screen's unanswered requests. Lifecycle events are raised
    /// per local screen, so a guest loading or leaving must not cancel callbacks owned by another
    /// screen in the same process.</summary>
    public void ResetCurrentScreen() => ResetScreen(Context.ScreenId);

    // ── Internals ────────────────────────────────────────────────────────────

    private void Track(Pending pending) => Track(Context.ScreenId, pending);

    internal void Track(int screenId, Pending pending) =>
        PendingForScreen(screenId)[pending.RequestId] = pending;

    internal void ResetScreen(int screenId) => _pendingByScreen.Remove(screenId);

    internal int PendingCount(int screenId) =>
        _pendingByScreen.TryGetValue(screenId, out var pending) ? pending.Count : 0;

    private Pending? Take(string requestId)
    {
        var pending = PendingForScreen(Context.ScreenId);
        if (!pending.TryGetValue(requestId, out var entry))
            return null;

        pending.Remove(requestId);
        return entry;
    }

    private Dictionary<string, Pending> PendingForScreen(int screenId)
    {
        if (!_pendingByScreen.TryGetValue(screenId, out var pending))
            _pendingByScreen[screenId] = pending = new Dictionary<string, Pending>(StringComparer.Ordinal);

        return pending;
    }

    private static string NewRequestId() => Guid.NewGuid().ToString("N");

    private bool CanDispatchToHost() =>
        !_suspension.HostIsUnverified
        && !_suspension.HostIsIncompatible
        && !_suspension.IsSuspended;

    private ContractRejectionCode HostUnavailableCode() =>
        _suspension.IsSuspended ? ContractRejectionCode.Paused : ContractRejectionCode.VersionMismatch;
}
