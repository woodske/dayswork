namespace Dayswork.Core.Net;

public enum PeerProtocolState
{
    Unverified,
    Compatible,
    Incompatible,
}

public sealed record PeerCompatibility(
    long PlayerId,
    PeerProtocolState State,
    DateTime DeadlineUtc,
    bool IsConnected,
    bool KickRequested,
    bool KickIssued,
    string PlayerName);

/// <summary>
/// Session-scoped protocol state for remote peers. Merely advertising the Dayswork mod ID is not
/// compatibility: a supported build remains unverified until it acknowledges the current wire
/// protocol, and a missed deadline becomes incompatible.
/// </summary>
public sealed class PeerCompatibilityRegistry
{
    private readonly Dictionary<long, PeerCompatibility> _peers = new();

    public void BeginVerification(long playerId, DateTime deadlineUtc, bool isConnected = false)
    {
        _peers[playerId] = new PeerCompatibility(
            playerId,
            PeerProtocolState.Unverified,
            deadlineUtc,
            isConnected,
            KickRequested: false,
            KickIssued: false,
            PlayerName: "");
    }

    public bool TryMarkCompatible(long playerId)
    {
        if (!_peers.TryGetValue(playerId, out var peer) || peer.State != PeerProtocolState.Unverified)
            return false;

        _peers[playerId] = peer with { State = PeerProtocolState.Compatible };
        return true;
    }

    public void MarkIncompatible(
        long playerId,
        string playerName,
        bool requestKick,
        bool isConnected)
    {
        var previous = Get(playerId);
        _peers[playerId] = new PeerCompatibility(
            playerId,
            PeerProtocolState.Incompatible,
            previous?.DeadlineUtc ?? DateTime.MinValue,
            isConnected || previous?.IsConnected == true,
            requestKick || previous?.KickRequested == true,
            previous?.KickIssued == true,
            playerName);
    }

    public void MarkConnected(long playerId)
    {
        if (_peers.TryGetValue(playerId, out var peer))
            _peers[playerId] = peer with { IsConnected = true };
    }

    /// <summary>True exactly once, after an incompatible peer is addressable by the transport.</summary>
    public bool TryMarkKickIssued(long playerId)
    {
        if (!_peers.TryGetValue(playerId, out var peer)
            || peer.State != PeerProtocolState.Incompatible
            || !peer.KickRequested
            || !peer.IsConnected
            || peer.KickIssued)
        {
            return false;
        }

        _peers[playerId] = peer with { KickIssued = true };
        return true;
    }

    public PeerCompatibility? Remove(long playerId)
    {
        if (!_peers.Remove(playerId, out var peer))
            return null;

        return peer;
    }

    public PeerCompatibility? Get(long playerId) =>
        _peers.TryGetValue(playerId, out var peer) ? peer : null;

    public bool IsCompatible(long playerId) => Get(playerId)?.State == PeerProtocolState.Compatible;

    public bool HasUnverifiedPeers => _peers.Values.Any(peer => peer.State == PeerProtocolState.Unverified);

    public IReadOnlyList<long> ExpiredUnverified(DateTime nowUtc) =>
        _peers.Values
            .Where(peer => peer.State == PeerProtocolState.Unverified && nowUtc >= peer.DeadlineUtc)
            .Select(peer => peer.PlayerId)
            .ToList();

    public void Reset() => _peers.Clear();
}
