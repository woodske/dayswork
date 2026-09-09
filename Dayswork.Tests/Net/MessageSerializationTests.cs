namespace Dayswork.Tests.Net;

using Dayswork.Core.Net;
using Newtonsoft.Json;
using Xunit;

/// <summary>
/// Round trips for every message DTO. SMAPI serializes mod messages with Newtonsoft, so a field
/// that does not survive JSON is a field the other end silently never receives — and because the
/// two ends may be different builds, "it works when I test it locally" proves nothing about the
/// shape actually going over the wire.
/// </summary>
public class MessageSerializationTests
{
    private static T RoundTrip<T>(T message) =>
        JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(message))!;

    [Fact]
    public void Hello_RoundTrips()
    {
        var result = RoundTrip(new HelloMessage
        {
            ProtocolVersion = 7,
            ModVersion = "2.0.0",
            Suspended = true,
            SuspensionReason = SuspensionReason.Version,
            SuspendedPlayerName = "Sam",
        });

        Assert.Equal(7, result.ProtocolVersion);
        Assert.Equal("2.0.0", result.ModVersion);
        Assert.True(result.Suspended);
        Assert.Equal(SuspensionReason.Version, result.SuspensionReason);
        Assert.Equal("Sam", result.SuspendedPlayerName);
    }

    [Fact]
    public void HelloAck_RoundTrips()
    {
        var result = RoundTrip(new HelloAckMessage { ProtocolVersion = 3, ModVersion = "1.9.9" });

        Assert.Equal(3, result.ProtocolVersion);
        Assert.Equal("1.9.9", result.ModVersion);
    }

    [Fact]
    public void Dayswork2Review_R10_HelloRoundTripsCurrentHostSuspension()
    {
        var result = RoundTrip(new HelloMessage
        {
            ProtocolVersion = DaysworkProtocol.Version,
            ModVersion = "2.0.0-beta.1",
            Suspended = true,
            SuspensionReason = SuspensionReason.NoMod,
            SuspendedPlayerName = "Late join blocker",
        });

        Assert.True(result.Suspended);
        Assert.Equal(SuspensionReason.NoMod, result.SuspensionReason);
        Assert.Equal("Late join blocker", result.SuspendedPlayerName);
    }

    [Fact]
    public void MenuSnapshot_RoundTripsItsLists()
    {
        var message = new MenuSnapshotResponseMessage
        {
            RequestId = "abc",
            ProtocolVersion = DaysworkProtocol.Version,
            OfficeId = "office-1",
            SpeedPurchased = true,
            Speed2Purchased = false,
            EnergyPurchased = true,
        };
        message.ExpansionLocations.Add(new SnapshotLocation
        {
            LocationName = "Custom_MountainFarm",
            DisplayName = "Mountain Farm",
            IsAvailable = true,
        });
        message.OffFarmChests.Add(new SnapshotChest
        {
            LocationName = "Custom_MountainFarm",
            LocationDisplayName = "Mountain Farm",
            TileX = 12,
            TileY = 34,
            DisplayName = "Seed chest",
            IsAutoGrabber = true,
        });

        var result = RoundTrip(message);

        Assert.Equal("abc", result.RequestId);
        Assert.True(result.SpeedPurchased);
        Assert.False(result.Speed2Purchased);
        Assert.True(result.EnergyPurchased);

        var location = Assert.Single(result.ExpansionLocations);
        Assert.Equal("Custom_MountainFarm", location.LocationName);
        Assert.True(location.IsAvailable);

        var chest = Assert.Single(result.OffFarmChests);
        Assert.Equal(12, chest.TileX);
        Assert.Equal(34, chest.TileY);
        Assert.Equal("Seed chest", chest.DisplayName);
        Assert.True(chest.IsAutoGrabber);
    }

    [Fact]
    public void CommitRequest_RoundTrips()
    {
        var result = RoundTrip(new ContractCommitRequestMessage
        {
            RequestId = "r1",
            ProtocolVersion = DaysworkProtocol.Version,
            OfficeId = "office-1",
            ContractJson = "{\"SchemaVersion\":4}",
            IsEdit = true,
            ExpectedRevision = 5,
        });

        Assert.Equal("r1", result.RequestId);
        Assert.Equal("{\"SchemaVersion\":4}", result.ContractJson);
        Assert.True(result.IsEdit);
        Assert.Equal(5, result.ExpectedRevision);
    }

    [Fact]
    public void CommitResponse_RoundTripsItsRejectionCode()
    {
        var result = RoundTrip(new ContractCommitResponseMessage
        {
            RequestId = "r1",
            Accepted = false,
            Code = ContractRejectionCode.ChestMissing,
            Detail = "Farm(3,4)",
        });

        Assert.False(result.Accepted);
        Assert.Equal(ContractRejectionCode.ChestMissing, result.Code);
        Assert.Equal("Farm(3,4)", result.Detail);
    }

    [Fact]
    public void CommitResponse_AcceptedCarriesNoCode()
    {
        var result = RoundTrip(new ContractCommitResponseMessage { RequestId = "r1", Accepted = true, Revision = 9 });

        Assert.True(result.Accepted);
        Assert.Equal(9, result.Revision);
        Assert.Null(result.Code);
    }

    [Fact]
    public void ActionRequestAndResponse_RoundTrip()
    {
        var request = RoundTrip(new ContractActionRequestMessage
        {
            RequestId = "a1",
            ProtocolVersion = DaysworkProtocol.Version,
            OfficeId = "office-1",
            Action = ContractActionKind.PurchaseUpgrade,
            UpgradeKind = "Speed2",
        });

        Assert.Equal(ContractActionKind.PurchaseUpgrade, request.Action);
        Assert.Equal("Speed2", request.UpgradeKind);

        var response = RoundTrip(new ContractActionResponseMessage
        {
            RequestId = "a1",
            Action = ContractActionKind.PurchaseUpgrade,
            Accepted = true,
            SpeedPurchased = true,
            Speed2Purchased = true,
        });

        Assert.True(response.Accepted);
        Assert.True(response.Speed2Purchased);
        Assert.False(response.EnergyPurchased);
    }

    [Fact]
    public void OwnerNotice_RoundTripsKeyAndTokens()
    {
        var result = RoundTrip(new OwnerNoticeMessage
        {
            TranslationKey = "notify.cannot_afford",
            Tokens = new Dictionary<string, string> { ["price"] = "500", ["shortfall"] = "120" },
            IsError = true,
        });

        Assert.Equal("notify.cannot_afford", result.TranslationKey);
        Assert.Equal("500", result.Tokens["price"]);
        Assert.Equal("120", result.Tokens["shortfall"]);
        Assert.True(result.IsError);
    }

    [Fact]
    public void Suspension_RoundTrips()
    {
        var result = RoundTrip(new SuspensionMessage { Reason = SuspensionReason.Version, PlayerName = "Sam" });

        Assert.Equal(SuspensionReason.Version, result.Reason);
        Assert.Equal("Sam", result.PlayerName);
    }
}
