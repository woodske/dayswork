using Dayswork.Core.Net;
using Dayswork.Net;
using Xunit;

namespace Dayswork.Tests.Net;

public sealed class PeerCompatibilityRegistryTests
{
    private static readonly DateTime Now = new(2026, 9, 8, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Dayswork2Review_R4_ModIdWithoutAcknowledgmentExpiresAsUnverified()
    {
        var peers = new PeerCompatibilityRegistry();
        peers.BeginVerification(10, Now.AddSeconds(10));

        Assert.True(peers.HasUnverifiedPeers);
        Assert.False(peers.IsCompatible(10));
        Assert.Empty(peers.ExpiredUnverified(Now.AddSeconds(9)));
        Assert.Equal(new[] { 10L }, peers.ExpiredUnverified(Now.AddSeconds(10)));
    }

    [Fact]
    public void Dayswork2Review_R4_MatchingProtocolAcceptsADifferentCompatiblePatchVersion()
    {
        var peers = new PeerCompatibilityRegistry();
        peers.BeginVerification(10, Now.AddSeconds(10), isConnected: true);

        var acknowledged = peers.TryMarkCompatible(10);

        Assert.True(acknowledged);
        Assert.True(peers.IsCompatible(10));
        Assert.False(peers.HasUnverifiedPeers);
        Assert.True(DaysworkNetwork.IsTypeCompatibleVersion("2.0.1"));
        Assert.True(DaysworkNetwork.IsTypeCompatibleVersion("2.1.0-beta.4"));
    }

    [Theory]
    [InlineData("1.2.1")]
    [InlineData("1.9.9")]
    [InlineData("")]
    public void Dayswork2Review_R4_PreProtocolVersionsAreKnownUnsafe(string version) =>
        Assert.False(DaysworkNetwork.IsTypeCompatibleVersion(version));

    [Fact]
    public void Dayswork2Review_R4_DisconnectBeforeAckClearsStateAndReconnectStartsFresh()
    {
        var peers = new PeerCompatibilityRegistry();
        peers.BeginVerification(10, Now.AddSeconds(10));

        var removed = peers.Remove(10);
        peers.BeginVerification(10, Now.AddSeconds(30), isConnected: true);

        Assert.Equal(PeerProtocolState.Unverified, removed?.State);
        Assert.False(peers.TryMarkCompatible(99));
        Assert.True(peers.TryMarkCompatible(10));
        Assert.True(peers.IsCompatible(10));
    }

    [Fact]
    public void Dayswork2Review_R4_WrongProtocolIsRejectedEvenForATypeCompatibleBuild()
    {
        Assert.True(DaysworkNetwork.IsTypeCompatibleVersion("2.0.0-beta.1"));
        Assert.True(DaysworkNetwork.IsProtocolCompatible(DaysworkProtocol.Version));
        Assert.False(DaysworkNetwork.IsProtocolCompatible(DaysworkProtocol.Version + 1));
    }

    [Fact]
    public void Dayswork2Review_R4_UnverifiedPeerBlocksNewExecutionWithoutBeginningSuspension()
    {
        var suspension = new DaysworkSuspension();
        var suspensionBegan = false;
        suspension.SuspensionBegan = () => suspensionBegan = true;

        suspension.MarkUnverified(10);

        Assert.True(suspension.IsExecutionBlocked);
        Assert.False(suspension.IsSuspended);
        Assert.False(suspensionBegan);

        suspension.MarkVerified(10);

        Assert.False(suspension.IsExecutionBlocked);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Dayswork2Review_R3_UnsafePeerCleansUpBeforeDeferredKick(bool kickPolicy)
    {
        var peers = new PeerCompatibilityRegistry();
        var suspension = new DaysworkSuspension();
        var liveWorker = true;
        suspension.SuspensionBegan = () => liveWorker = false;

        peers.MarkIncompatible(10, "Guest", kickPolicy, isConnected: false);
        suspension.MarkIncompatible(10, SuspensionReason.NoMod, "Guest");

        Assert.False(liveWorker);
        Assert.False(peers.TryMarkKickIssued(10));

        peers.MarkConnected(10);
        Assert.Equal(kickPolicy, peers.TryMarkKickIssued(10));
        Assert.False(peers.TryMarkKickIssued(10));

        var removed = peers.Remove(10);
        suspension.ForgetPeer(10);
        Assert.Equal(kickPolicy, removed?.KickIssued);
        Assert.Null(peers.Get(10));
        Assert.False(suspension.IsSuspended);
    }

    [Fact]
    public void Dayswork2Review_R10_RemoteSuspensionStateClearsAcrossSessions()
    {
        var suspension = new DaysworkSuspension
        {
            HostIsUnverified = false,
            HostIsIncompatible = false,
        };
        suspension.ApplyRemoteState(true, SuspensionReason.Version, "Legacy guest");

        Assert.True(suspension.IsSuspended);
        Assert.Equal(SuspensionReason.Version, suspension.Reason);
        Assert.Equal("Legacy guest", suspension.PlayerName);

        suspension.Reset();

        Assert.False(suspension.IsSuspended);
        Assert.False(suspension.HostIsIncompatible);
        Assert.False(suspension.HostIsUnverified);
        Assert.Equal("", suspension.PlayerName);
    }

    [Fact]
    public void Dayswork2Review_R10_LateJoinerMirrorsHostStateAndBothClientsResumeTogether()
    {
        var host = new DaysworkSuspension();
        var firstClient = new DaysworkSuspension();
        var lateClient = new DaysworkSuspension();
        host.SuspensionEnded = () =>
        {
            firstClient.ApplyRemoteState(false, SuspensionReason.NoMod, "");
            lateClient.ApplyRemoteState(false, SuspensionReason.NoMod, "");
        };

        host.MarkIncompatible(10, SuspensionReason.Version, "Old build");
        firstClient.ApplyRemoteState(host.IsSuspended, host.Reason, host.PlayerName);
        lateClient.ApplyRemoteState(host.IsSuspended, host.Reason, host.PlayerName);

        Assert.True(firstClient.IsSuspended);
        Assert.True(lateClient.IsSuspended);
        Assert.Equal("Old build", firstClient.PlayerName);
        Assert.Equal(firstClient.PlayerName, lateClient.PlayerName);

        host.ForgetPeer(10);

        Assert.False(firstClient.IsSuspended);
        Assert.False(lateClient.IsSuspended);
    }
}
