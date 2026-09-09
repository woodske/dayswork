using Dayswork.Core.Net;
using Dayswork.Core.Persistence;
using Dayswork.Guards;
using Dayswork.Integration;
using Dayswork.Orchestration;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace Dayswork.Net;

/// <summary>
/// The multiplayer seam: peer handshake, the incompatible-peer policy, message dispatch, and the
/// owner-disconnect rule. One subscriber per event, fanning out to the pieces that care — the same
/// shape <see cref="ShiftFleet"/> uses for the shift events.
/// </summary>
internal sealed class DaysworkNetwork
{
    private static readonly TimeSpan HandshakeTimeout = TimeSpan.FromSeconds(10);

    private readonly IModHelper _helper;
    private readonly NetChannel _channel;
    private readonly ContractRequestHandler _handler;
    private readonly ContractRequestClient _client;
    private readonly DaysworkSuspension _suspension;
    private readonly ModConfigManager _configManager;
    private readonly OfficeContractStore _store;
    private readonly ShiftFleet _fleet;
    private readonly string _modVersion;
    private readonly PeerCompatibilityRegistry _peers = new();

    public DaysworkNetwork(
        IModHelper helper,
        NetChannel channel,
        ContractRequestHandler handler,
        ContractRequestClient client,
        DaysworkSuspension suspension,
        ModConfigManager configManager,
        OfficeContractStore store,
        ShiftFleet fleet,
        string modVersion)
    {
        _helper = helper;
        _channel = channel;
        _handler = handler;
        _client = client;
        _suspension = suspension;
        _configManager = configManager;
        _store = store;
        _fleet = fleet;
        _modVersion = modVersion;

        // Suspension stops the world for Dayswork: live workers come home the moment an
        // incompatible peer is detected, and nothing new spawns until they have gone.
        _suspension.SuspensionBegan = OnSuspensionBegan;
        _suspension.SuspensionEnded = OnSuspensionEnded;

        // The host is the only peer that can deliver a notice to somebody else.
        OwnerNotifier.RemoteSender = SendOwnerNotice;
    }

    // ── Handshake ────────────────────────────────────────────────────────────

    /// <summary>
    /// The earliest point at which the host learns anything about a joining peer — crucially,
    /// before the game approves the connection, which is the only window in which a worker can be
    /// despawned before the world snapshot goes out (2.0 plan D8 and correction 8).
    /// </summary>
    public void OnPeerContextReceived(object? sender, PeerContextReceivedEventArgs e)
    {
        if (!Authority.IsHost)
        {
            if (!Authority.IsSplitScreenGuest && e.Peer.IsHost)
                ObserveHostFromClient(e.Peer);
            return;
        }

        ObservePeerFromHost(e.Peer, isConnected: false);
    }

    /// <summary>A peer that got this far without SMAPI never raised a mod context, so this is where
    /// a vanilla guest is caught.</summary>
    public void OnPeerConnected(object? sender, PeerConnectedEventArgs e)
    {
        if (!Authority.IsHost)
        {
            if (!Authority.IsSplitScreenGuest && e.Peer.IsHost)
                ObserveHostFromClient(e.Peer);
            return;
        }

        ObservePeerFromHost(e.Peer, isConnected: true);
        IssuePendingKick(e.Peer.PlayerID);
    }

    public void OnPeerDisconnected(object? sender, PeerDisconnectedEventArgs e)
    {
        if (!Authority.IsHost)
        {
            if (!Authority.IsSplitScreenGuest && e.Peer.IsHost)
            {
                _peers.Remove(e.Peer.PlayerID);
                _suspension.Reset();
            }
            return;
        }

        var removed = _peers.Remove(e.Peer.PlayerID);
        // Only now do we know the transport actually disconnected after our approved-peer kick.
        if (removed?.KickIssued == true)
            AnnounceToEveryone(I18nHelper.Get("ui.net.kicked", new { player = removed.PlayerName }));

        _suspension.ForgetPeer(e.Peer.PlayerID);
        EndShiftsForDepartedOwner(e.Peer.PlayerID);
    }

    /// <summary>
    /// An owner who leaves mid-shift: their farmhand carries on by default (it is already out and
    /// working with the tool snapshot taken at shift start), unless their contract says otherwise,
    /// in which case the shift ends early through the normal path so nothing they collected is
    /// stranded.
    /// </summary>
    private void EndShiftsForDepartedOwner(long ownerId)
    {
        foreach (var (officeId, shiftOwnerId) in _fleet.LiveShifts())
        {
            if (shiftOwnerId != ownerId)
                continue;
            if (_store.ForOffice(officeId)?.Preferences.RunWhileOwnerOffline != false)
                continue;

            _fleet.ForOffice(officeId)?.EndShiftEarly();
            ModEntry.ModMonitor.Log(
                $"[Dayswork] Owner {ownerId} disconnected and their contract does not run while they are offline — ending the shift at office {officeId:N}.",
                DevLog.WarnLevel);
        }
    }

    private void HandleIncompatiblePeer(long playerId, SuspensionReason reason, bool isConnected = false)
    {
        var playerName = Sponsor.Resolve(playerId)?.Name ?? I18nHelper.Get("ui.net.unknown_player");
        var kickRequested = _configManager.Editable.KickIncompatiblePeers;
        isConnected |= _peers.Get(playerId)?.IsConnected == true;

        _peers.MarkIncompatible(playerId, playerName, kickRequested, isConnected);
        _suspension.MarkIncompatible(playerId, reason, playerName);
        IssuePendingKick(playerId);
    }

    private void IssuePendingKick(long playerId)
    {
        if (Game1.server is null || !_peers.TryMarkKickIssued(playerId))
            return;

        Game1.server.kick(playerId);
        ModEntry.ModMonitor.Log(
            $"[Dayswork] Requested disconnect for approved incompatible player {playerId}; awaiting the disconnect event.",
            DevLog.WarnLevel);
    }

    private void ObservePeerFromHost(IMultiplayerPeer peer, bool isConnected)
    {
        // A local split-screen guest runs this exact assembly in the host process, so there is no
        // wire handshake to prove and no independent worker type that could be incompatible.
        if (peer.IsSplitScreen)
        {
            _peers.BeginVerification(peer.PlayerID, DateTime.MaxValue, isConnected);
            _peers.TryMarkCompatible(peer.PlayerID);
            _suspension.MarkVerified(peer.PlayerID);
            return;
        }

        var mod = peer.HasSmapi ? peer.GetMod(DaysworkProtocol.ModId) : null;
        if (mod is null)
        {
            HandleIncompatiblePeer(peer.PlayerID, SuspensionReason.NoMod, isConnected);
            return;
        }

        // 1.x used the same mod ID but had neither the custom-type protocol nor a Hello handler.
        // It is known unsafe before approval, unlike a 2.x build awaiting protocol confirmation.
        if (!IsTypeCompatibleVersion(mod.Version.ToString()))
        {
            HandleIncompatiblePeer(peer.PlayerID, SuspensionReason.Version, isConnected);
            return;
        }

        var existing = _peers.Get(peer.PlayerID);
        if (existing?.State == PeerProtocolState.Compatible)
        {
            if (isConnected)
                _peers.MarkConnected(peer.PlayerID);
            return;
        }
        if (existing?.State == PeerProtocolState.Incompatible)
        {
            if (isConnected)
                _peers.MarkConnected(peer.PlayerID);
            return;
        }

        if (existing is null)
        {
            _peers.BeginVerification(peer.PlayerID, DateTime.UtcNow + HandshakeTimeout, isConnected);
            _suspension.MarkUnverified(peer.PlayerID);
        }
        else if (isConnected)
        {
            _peers.MarkConnected(peer.PlayerID);
        }

        SendHello(peer.PlayerID);
    }

    private void ObserveHostFromClient(IMultiplayerPeer host)
    {
        var mod = host.HasSmapi ? host.GetMod(DaysworkProtocol.ModId) : null;
        if (mod is null || !IsTypeCompatibleVersion(mod.Version.ToString()))
        {
            _peers.MarkIncompatible(
                host.PlayerID,
                playerName: "",
                requestKick: false,
                isConnected: true);
            _suspension.HostIsUnverified = false;
            _suspension.HostIsIncompatible = true;
            return;
        }

        if (_peers.Get(host.PlayerID)?.State is PeerProtocolState.Compatible or PeerProtocolState.Incompatible)
            return;

        _peers.BeginVerification(host.PlayerID, DateTime.UtcNow + HandshakeTimeout, isConnected: true);
        _suspension.HostIsUnverified = true;
        _suspension.HostIsIncompatible = false;
    }

    private void SendHello(long playerId) =>
        _channel.Send(DaysworkProtocol.Hello, new HelloMessage
        {
            ProtocolVersion = DaysworkProtocol.Version,
            ModVersion = _modVersion,
            Suspended = _suspension.IsSuspended,
            SuspensionReason = _suspension.Reason,
            SuspendedPlayerName = _suspension.PlayerName,
        }, playerId);

    private void SendCurrentSuspensionState(long playerId)
    {
        if (_suspension.IsSuspended)
        {
            _channel.Send(DaysworkProtocol.DaysworkPaused, new SuspensionMessage
            {
                Reason = _suspension.Reason,
                PlayerName = _suspension.PlayerName,
            }, playerId);
        }
        else
        {
            _channel.Send(DaysworkProtocol.DaysworkResumed, new SuspensionMessage(), playerId);
        }
    }

    internal static bool IsTypeCompatibleVersion(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
            return false;

        var majorText = new string(version.TakeWhile(char.IsDigit).ToArray());
        return int.TryParse(majorText, out var major) && major >= 2;
    }

    internal static bool IsProtocolCompatible(int protocolVersion) =>
        protocolVersion == DaysworkProtocol.Version;

    private void OnSuspensionBegan()
    {
        _fleet.StopForSleepAndSettle();

        var text = I18nHelper.Get(
            _suspension.Reason == SuspensionReason.NoMod ? "ui.net.suspended_no_mod" : "ui.net.suspended_version",
            new { player = _suspension.PlayerName });
        AnnounceToEveryone(text);

        _channel.Broadcast(DaysworkProtocol.DaysworkPaused, new SuspensionMessage
        {
            Reason = _suspension.Reason,
            PlayerName = _suspension.PlayerName,
        });

        ModEntry.ModMonitor.Log($"[Dayswork] Suspended: {text}", DevLog.WarnLevel);
    }

    private void OnSuspensionEnded()
    {
        AnnounceToEveryone(I18nHelper.Get("ui.net.resumed"));
        _channel.Broadcast(DaysworkProtocol.DaysworkResumed, new SuspensionMessage());
        ModEntry.ModMonitor.Log("[Dayswork] Resumed: no incompatible peers remain.", DevLog.WarnLevel);
    }

    /// <summary>
    /// A chat line, because it is the only channel a guest without the mod can read. The host's own
    /// copy has to be added locally: <c>sendChatMessage</c> only reaches the other farmers
    /// (verified against the decompile, 2026-09-07).
    /// </summary>
    private static void AnnounceToEveryone(string text)
    {
        Game1.chatBox?.addInfoMessage(text);
        if (Context.IsMultiplayer)
        {
            Game1.Multiplayer?.sendChatMessage(
                LocalizedContentManager.CurrentLanguageCode,
                text,
                StardewValley.Multiplayer.AllPlayers);
        }
    }

    // ── Message dispatch ─────────────────────────────────────────────────────

    public void OnModMessageReceived(object? sender, ModMessageReceivedEventArgs e)
    {
        if (!string.Equals(e.FromModID, DaysworkProtocol.ModId, StringComparison.Ordinal))
            return;

        try
        {
            Dispatch(e);
        }
        catch (Exception ex)
        {
            ModEntry.ModMonitor.Log(
                $"[Dayswork] Failed to handle a '{e.Type}' message from player {e.FromPlayerID}: {ex.Message}",
                DevLog.WarnLevel);
        }
    }

    private void Dispatch(ModMessageReceivedEventArgs e)
    {
        switch (e.Type)
        {
            // ── Host side ────────────────────────────────────────────────────
            case DaysworkProtocol.HelloAck when Authority.IsHost:
            {
                var ack = e.ReadAs<HelloAckMessage>();
                if (_peers.Get(e.FromPlayerID)?.State != PeerProtocolState.Unverified)
                    break;

                if (!IsProtocolCompatible(ack.ProtocolVersion))
                {
                    HandleIncompatiblePeer(e.FromPlayerID, SuspensionReason.Version);
                    break;
                }

                _peers.TryMarkCompatible(e.FromPlayerID);
                _suspension.MarkVerified(e.FromPlayerID);
                SendCurrentSuspensionState(e.FromPlayerID);
                break;
            }

            case DaysworkProtocol.ContractCommitRequest when Authority.IsHost:
            {
                var request = e.ReadAs<ContractCommitRequestMessage>();
                if (!_peers.IsCompatible(e.FromPlayerID))
                {
                    RejectUnverifiedCommit(request, e.FromPlayerID);
                    break;
                }
                _channel.Send(
                    DaysworkProtocol.ContractCommitResponse,
                    _handler.HandleCommit(request, e.FromPlayerID),
                    e.FromPlayerID);
                break;
            }

            case DaysworkProtocol.ContractActionRequest when Authority.IsHost:
            {
                var request = e.ReadAs<ContractActionRequestMessage>();
                if (!_peers.IsCompatible(e.FromPlayerID))
                {
                    RejectUnverifiedAction(request, e.FromPlayerID);
                    break;
                }
                _channel.Send(
                    DaysworkProtocol.ContractActionResponse,
                    _handler.HandleAction(request, e.FromPlayerID),
                    e.FromPlayerID);
                break;
            }

            case DaysworkProtocol.MenuSnapshotRequest when Authority.IsHost:
            {
                var request = e.ReadAs<MenuSnapshotRequestMessage>();
                if (!_peers.IsCompatible(e.FromPlayerID))
                    break;
                _channel.Send(
                    DaysworkProtocol.MenuSnapshotResponse,
                    _handler.HandleSnapshotRequest(request, e.FromPlayerID),
                    e.FromPlayerID);
                break;
            }

            // ── Client side ──────────────────────────────────────────────────
            case DaysworkProtocol.Hello when !Authority.IsHost && IsFromHost(e):
            {
                var hello = e.ReadAs<HelloMessage>();
                if (!IsProtocolCompatible(hello.ProtocolVersion))
                {
                    _peers.MarkIncompatible(
                        e.FromPlayerID,
                        playerName: "",
                        requestKick: false,
                        isConnected: true);
                    _suspension.HostIsUnverified = false;
                    _suspension.HostIsIncompatible = true;
                    break;
                }

                if (_peers.Get(e.FromPlayerID) is null)
                    _peers.BeginVerification(e.FromPlayerID, DateTime.UtcNow + HandshakeTimeout, isConnected: true);
                _peers.TryMarkCompatible(e.FromPlayerID);
                _suspension.HostIsUnverified = false;
                _suspension.HostIsIncompatible = false;
                _suspension.ApplyRemoteState(
                    hello.Suspended,
                    hello.SuspensionReason,
                    hello.SuspendedPlayerName);
                _channel.Send(DaysworkProtocol.HelloAck, new HelloAckMessage
                {
                    ProtocolVersion = DaysworkProtocol.Version,
                    ModVersion = _modVersion,
                }, e.FromPlayerID);
                break;
            }

            case DaysworkProtocol.ContractCommitResponse when !Authority.IsHost && IsFromHost(e):
                _client.ReceiveCommitResponse(e.ReadAs<ContractCommitResponseMessage>());
                break;

            case DaysworkProtocol.ContractActionResponse when !Authority.IsHost && IsFromHost(e):
                _client.ReceiveActionResponse(e.ReadAs<ContractActionResponseMessage>());
                break;

            case DaysworkProtocol.MenuSnapshotResponse when !Authority.IsHost && IsFromHost(e):
            {
                var snapshot = e.ReadAs<MenuSnapshotResponseMessage>();
                if (snapshot.ProtocolVersion == DaysworkProtocol.Version)
                    MenuSnapshotCache.ApplySnapshot(snapshot);
                break;
            }

            case DaysworkProtocol.OwnerNotice when !Authority.IsHost && IsFromHost(e):
            {
                var notice = e.ReadAs<OwnerNoticeMessage>();
                OwnerNotifier.ShowReceived(notice.TranslationKey, notice.Tokens, notice.IsError);
                break;
            }

            case DaysworkProtocol.DaysworkPaused when !Authority.IsHost && IsFromHost(e):
            {
                var message = e.ReadAs<SuspensionMessage>();
                _suspension.ApplyRemoteState(true, message.Reason, message.PlayerName);
                break;
            }

            case DaysworkProtocol.DaysworkResumed when !Authority.IsHost && IsFromHost(e):
                _suspension.ApplyRemoteState(false, SuspensionReason.NoMod, "");
                break;
        }
    }

    private static bool IsFromHost(ModMessageReceivedEventArgs e) =>
        e.FromPlayerID == Game1.MasterPlayer.UniqueMultiplayerID;

    private void RejectUnverifiedCommit(ContractCommitRequestMessage request, long playerId) =>
        _channel.Send(
            DaysworkProtocol.ContractCommitResponse,
            new ContractCommitResponseMessage
            {
                RequestId = request.RequestId,
                Accepted = false,
                Code = ContractRejectionCode.VersionMismatch,
                Detail = PeerProtocolState.Unverified.ToString(),
            },
            playerId);

    private void RejectUnverifiedAction(ContractActionRequestMessage request, long playerId) =>
        _channel.Send(
            DaysworkProtocol.ContractActionResponse,
            new ContractActionResponseMessage
            {
                RequestId = request.RequestId,
                Action = request.Action,
                Accepted = false,
                Code = ContractRejectionCode.VersionMismatch,
                Detail = PeerProtocolState.Unverified.ToString(),
            },
            playerId);

    /// <summary>
    /// Everything the protocol remembers about one session. Both hooks fire once per screen, so the
    /// host-owned pieces are reset only from the host's own screen — a split-screen guest loading in
    /// must not wipe the request ids the host is using to deduplicate.
    /// </summary>
    private void ResetSessionState()
    {
        _client.ResetCurrentScreen();
        MenuSnapshotCache.ClearCurrentScreen();
        // Sequence numbers are per host session and start again from one, so a stale high-water
        // mark from the last session would reject every state of the new one (R7).
        AuthoritativeContractCache.ClearCurrentScreen();

        if (Authority.IsHost)
        {
            _peers.Reset();
            _suspension.Reset();
            _handler.Reset();
        }
        else if (Authority.IsRemoteClient)
        {
            _peers.Reset();
            _suspension.Reset();
        }
    }

    private void SendOwnerNotice(long ownerId, string key, IReadOnlyDictionary<string, string> tokens, bool isError)
    {
        if (!Authority.IsHost)
            return;

        _channel.Send(DaysworkProtocol.OwnerNotice, new OwnerNoticeMessage
        {
            TranslationKey = key,
            Tokens = new Dictionary<string, string>(tokens),
            IsError = isError,
        }, ownerId);
    }

    // ── Session boundaries ───────────────────────────────────────────────────

    public void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e) => ResetSessionState();

    public void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
    {
        if (!e.IsMultipleOf(60) || Authority.IsSplitScreenGuest)
            return;

        foreach (var playerId in _peers.ExpiredUnverified(DateTime.UtcNow))
        {
            if (Authority.IsHost)
            {
                HandleIncompatiblePeer(playerId, SuspensionReason.Version);
            }
            else
            {
                _peers.MarkIncompatible(
                    playerId,
                    playerName: "",
                    requestKick: false,
                    isConnected: true);
                _suspension.HostIsUnverified = false;
                _suspension.HostIsIncompatible = true;
            }
        }
    }

    /// <summary>
    /// Re-checks every peer already connected when this save loaded. A client joining a session
    /// that is already underway learns about the host here; the host re-derives its incompatible
    /// set rather than trusting one carried over from the previous save.
    /// </summary>
    public void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
    {
        ResetSessionState();

        if (Authority.IsSplitScreenGuest)
            return;

        if (Authority.IsRemoteClient)
        {
            var hostId = Game1.MasterPlayer.UniqueMultiplayerID;
            _peers.BeginVerification(hostId, DateTime.UtcNow + HandshakeTimeout, isConnected: true);
            _suspension.HostIsUnverified = true;
        }

        foreach (var peer in _helper.Multiplayer.GetConnectedPlayers())
        {
            if (Authority.IsHost)
                ObservePeerFromHost(peer, isConnected: true);
            else if (peer.IsHost)
                ObserveHostFromClient(peer);
        }
    }
}
