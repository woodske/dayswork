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
    private readonly IModHelper _helper;
    private readonly NetChannel _channel;
    private readonly ContractRequestHandler _handler;
    private readonly ContractRequestClient _client;
    private readonly DaysworkSuspension _suspension;
    private readonly ModConfigManager _configManager;
    private readonly OfficeContractStore _store;
    private readonly ShiftFleet _fleet;
    private readonly string _modVersion;

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
            // Client side: the peer we care about is the host. No Dayswork there means none of our
            // UI can do anything, so it says so rather than failing silently.
            if (e.Peer.IsHost && e.Peer.GetMod(DaysworkProtocol.ModId) is null)
                _suspension.HostIsIncompatible = true;
            return;
        }

        if (!e.Peer.HasSmapi || e.Peer.GetMod(DaysworkProtocol.ModId) is null)
        {
            HandleIncompatiblePeer(e.Peer.PlayerID, SuspensionReason.NoMod);
            return;
        }

        // They have the mod; whether they speak our protocol version is what Hello establishes.
        _channel.Send(DaysworkProtocol.Hello, new HelloMessage
        {
            ProtocolVersion = DaysworkProtocol.Version,
            ModVersion = _modVersion,
        }, e.Peer.PlayerID);
    }

    /// <summary>A peer that got this far without SMAPI never raised a mod context, so this is where
    /// a vanilla guest is caught.</summary>
    public void OnPeerConnected(object? sender, PeerConnectedEventArgs e)
    {
        if (!Authority.IsHost)
            return;

        if (!e.Peer.HasSmapi || e.Peer.GetMod(DaysworkProtocol.ModId) is null)
            HandleIncompatiblePeer(e.Peer.PlayerID, SuspensionReason.NoMod);
    }

    public void OnPeerDisconnected(object? sender, PeerDisconnectedEventArgs e)
    {
        if (!Authority.IsHost)
            return;

        _suspension.MarkCompatible(e.Peer.PlayerID);
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

    private void HandleIncompatiblePeer(long playerId, SuspensionReason reason)
    {
        if (_suspension.IsIncompatible(playerId))
            return;

        var playerName = Sponsor.Resolve(playerId)?.Name ?? I18nHelper.Get("ui.net.unknown_player");

        if (_configManager.Editable.KickIncompatiblePeers)
        {
            AnnounceToEveryone(I18nHelper.Get("ui.net.kicked", new { player = playerName }));
            Game1.server?.kick(playerId);
            ModEntry.ModMonitor.Log(
                $"[Dayswork] Kicked player {playerId} ({reason}) — KickIncompatiblePeers is on.",
                DevLog.WarnLevel);
            return;
        }

        _suspension.MarkIncompatible(playerId, reason, playerName);
    }

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
                if (ack.ProtocolVersion != DaysworkProtocol.Version)
                    HandleIncompatiblePeer(e.FromPlayerID, SuspensionReason.Version);
                break;
            }

            case DaysworkProtocol.ContractCommitRequest when Authority.IsHost:
            {
                var request = e.ReadAs<ContractCommitRequestMessage>();
                _channel.Send(
                    DaysworkProtocol.ContractCommitResponse,
                    _handler.HandleCommit(request, e.FromPlayerID),
                    e.FromPlayerID);
                break;
            }

            case DaysworkProtocol.ContractActionRequest when Authority.IsHost:
            {
                var request = e.ReadAs<ContractActionRequestMessage>();
                _channel.Send(
                    DaysworkProtocol.ContractActionResponse,
                    _handler.HandleAction(request, e.FromPlayerID),
                    e.FromPlayerID);
                break;
            }

            case DaysworkProtocol.MenuSnapshotRequest when Authority.IsHost:
            {
                var request = e.ReadAs<MenuSnapshotRequestMessage>();
                _channel.Send(
                    DaysworkProtocol.MenuSnapshotResponse,
                    _handler.HandleSnapshotRequest(request, e.FromPlayerID),
                    e.FromPlayerID);
                break;
            }

            // ── Client side ──────────────────────────────────────────────────
            case DaysworkProtocol.Hello:
            {
                var hello = e.ReadAs<HelloMessage>();
                _suspension.HostIsIncompatible = hello.ProtocolVersion != DaysworkProtocol.Version;
                _channel.Send(DaysworkProtocol.HelloAck, new HelloAckMessage
                {
                    ProtocolVersion = DaysworkProtocol.Version,
                    ModVersion = _modVersion,
                    Suspended = _suspension.IsSuspended,
                }, e.FromPlayerID);
                break;
            }

            case DaysworkProtocol.ContractCommitResponse:
                _client.ReceiveCommitResponse(e.ReadAs<ContractCommitResponseMessage>());
                break;

            case DaysworkProtocol.ContractActionResponse:
                _client.ReceiveActionResponse(e.ReadAs<ContractActionResponseMessage>());
                break;

            case DaysworkProtocol.MenuSnapshotResponse:
                MenuSnapshotCache.Current = e.ReadAs<MenuSnapshotResponseMessage>();
                break;

            case DaysworkProtocol.OwnerNotice:
            {
                var notice = e.ReadAs<OwnerNoticeMessage>();
                OwnerNotifier.ShowReceived(notice.TranslationKey, notice.Tokens, notice.IsError);
                break;
            }

            case DaysworkProtocol.DaysworkPaused:
            {
                var message = e.ReadAs<SuspensionMessage>();
                _suspension.ApplyRemoteState(true, message.Reason, message.PlayerName);
                break;
            }

            case DaysworkProtocol.DaysworkResumed:
                _suspension.ApplyRemoteState(false, SuspensionReason.NoMod, "");
                break;
        }
    }

    /// <summary>
    /// Everything the protocol remembers about one session. Both hooks fire once per screen, so the
    /// host-owned pieces are reset only from the host's own screen — a split-screen guest loading in
    /// must not wipe the request ids the host is using to deduplicate.
    /// </summary>
    private void ResetSessionState()
    {
        _client.Reset();
        MenuSnapshotCache.Current = null;

        if (Authority.HostIsInThisProcess && Authority.IsHost)
        {
            _suspension.Reset();
            _handler.Reset();
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

    /// <summary>
    /// Re-checks every peer already connected when this save loaded. A client joining a session
    /// that is already underway learns about the host here; the host re-derives its incompatible
    /// set rather than trusting one carried over from the previous save.
    /// </summary>
    public void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
    {
        ResetSessionState();

        foreach (var peer in _helper.Multiplayer.GetConnectedPlayers())
        {
            if (Authority.IsHost)
            {
                if (!peer.HasSmapi || peer.GetMod(DaysworkProtocol.ModId) is null)
                    HandleIncompatiblePeer(peer.PlayerID, SuspensionReason.NoMod);
            }
            else if (peer.IsHost && peer.GetMod(DaysworkProtocol.ModId) is null)
            {
                _suspension.HostIsIncompatible = true;
            }
        }
    }
}
