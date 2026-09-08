using Dayswork.Core.Domain;
using Dayswork.Core.Net;
using Dayswork.Core.Persistence;
using Dayswork.Guards;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
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

    private sealed record Pending(
        string RequestId,
        DateTime SentAt,
        Action<ContractCommitResponseMessage>? OnCommit,
        Action<ContractActionResponseMessage>? OnAction);

    private readonly NetChannel _channel;
    private readonly ContractRequestHandler _handler;
    private readonly SaveDataSerializer _serializer;
    private readonly string _modVersion;

    // Per screen: on a remote machine running split-screen, two guests each have their own menu
    // and their own outstanding request.
    private readonly PerScreen<Dictionary<string, Pending>> _pending = new(() => new Dictionary<string, Pending>(StringComparer.Ordinal));

    public ContractRequestClient(
        NetChannel channel,
        ContractRequestHandler handler,
        SaveDataSerializer serializer,
        string modVersion)
    {
        _channel = channel;
        _handler = handler;
        _serializer = serializer;
        _modVersion = modVersion;
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
        if (!Authority.IsRemoteClient)
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
        if (Take(response.RequestId) is { } pending)
            pending.OnCommit?.Invoke(response);
    }

    public void ReceiveActionResponse(ContractActionResponseMessage response)
    {
        if (Take(response.RequestId) is { } pending)
            pending.OnAction?.Invoke(response);
    }

    /// <summary>
    /// Drops requests the host never answered. The host may in fact have committed — the next time
    /// the hub opens it reads the office's modData and shows the truth — so the player is told that
    /// their changes were not confirmed rather than that they were lost.
    /// </summary>
    public void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
    {
        var pending = _pending.Value;
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

    public void Reset() => _pending.ResetAllScreens();

    // ── Internals ────────────────────────────────────────────────────────────

    private void Track(Pending pending) => _pending.Value[pending.RequestId] = pending;

    private Pending? Take(string requestId)
    {
        var pending = _pending.Value;
        if (!pending.TryGetValue(requestId, out var entry))
            return null;

        pending.Remove(requestId);
        return entry;
    }

    private static string NewRequestId() => Guid.NewGuid().ToString("N");
}
