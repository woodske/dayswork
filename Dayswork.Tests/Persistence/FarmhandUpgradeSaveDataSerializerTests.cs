namespace Dayswork.Tests.Persistence;

using Dayswork.Core.Persistence;
using Dayswork.Core.Upgrades;
using Newtonsoft.Json.Linq;
using Xunit;

public sealed class FarmhandUpgradeSaveDataSerializerTests
{
    private const long HostId = 1000L;
    private const long GuestId = 2000L;

    private readonly List<string> _warnings = new();
    private readonly FarmhandUpgradeSaveDataSerializer _serializer;

    public FarmhandUpgradeSaveDataSerializerTests()
    {
        _serializer = new FarmhandUpgradeSaveDataSerializer(_warnings.Add);
    }

    [Fact]
    public void Deserialize_MissingPayload_DefaultsToNoUpgrades()
    {
        var state = _serializer.Deserialize(null, HostId);

        Assert.Empty(state);
        Assert.Empty(_warnings);
    }

    [Fact]
    public void SerializeAndDeserialize_PurchasedUpgrades_RoundTripPerOwner()
    {
        var owners = new Dictionary<long, FarmhandUpgradeState>
        {
            [HostId] = new(SpeedPurchased: true, EnergyPurchased: true, Speed2Purchased: true),
            [GuestId] = new(SpeedPurchased: true, EnergyPurchased: false, Speed2Purchased: false),
        };

        var hydrated = _serializer.Deserialize(_serializer.Serialize(owners), HostId);

        Assert.Equal(owners[HostId], hydrated[HostId]);
        Assert.Equal(owners[GuestId], hydrated[GuestId]);
        Assert.Equal(2, hydrated.Count);
    }

    [Fact]
    public void Deserialize_LegacyV1Payload_MigratesToTheHostWithSecondSpeedTierUnowned()
    {
        // A v1 save predates the second speed upgrade — it has no Speed2Purchased field — and
        // predates per-owner upgrades, so its one global set can only have been the host's.
        var payload = new JObject
        {
            ["SchemaVersion"] = 1,
            ["SpeedPurchased"] = true,
            ["EnergyPurchased"] = true,
        };

        var state = _serializer.Deserialize(payload.ToString(), HostId);

        Assert.True(state[HostId].SpeedPurchased);
        Assert.True(state[HostId].EnergyPurchased);
        Assert.False(state[HostId].Speed2Purchased);
        Assert.Single(state);
        Assert.Empty(_warnings);
    }

    [Fact]
    public void Deserialize_LegacyV2Payload_MigratesToTheHost()
    {
        var payload = new JObject
        {
            ["SchemaVersion"] = 2,
            ["SpeedPurchased"] = true,
            ["EnergyPurchased"] = false,
            ["Speed2Purchased"] = true,
        };

        var state = _serializer.Deserialize(payload.ToString(), HostId);

        Assert.Equal(new FarmhandUpgradeState(true, false, true), state[HostId]);
        Assert.Single(state);
        Assert.Empty(_warnings);
    }

    [Fact]
    public void Deserialize_LegacyPayloadWithNothingPurchased_StoresNoEntry()
    {
        var payload = new JObject
        {
            ["SchemaVersion"] = 2,
            ["SpeedPurchased"] = false,
            ["EnergyPurchased"] = false,
            ["Speed2Purchased"] = false,
        };

        var state = _serializer.Deserialize(payload.ToString(), HostId);

        Assert.Empty(state);
    }

    [Fact]
    public void Deserialize_NonNumericOwnerKey_SkipsItAndWarns()
    {
        var payload = new JObject
        {
            ["SchemaVersion"] = 3,
            ["Owners"] = new JObject
            {
                ["not-a-player"] = new JObject { ["SpeedPurchased"] = true },
                [HostId.ToString()] = new JObject { ["EnergyPurchased"] = true },
            },
        };

        var state = _serializer.Deserialize(payload.ToString(), HostId);

        Assert.Single(state);
        Assert.True(state[HostId].EnergyPurchased);
        Assert.Single(_warnings);
    }

    [Fact]
    public void Deserialize_MalformedPayload_DefaultsToNoUpgradesAndWarns()
    {
        var state = _serializer.Deserialize("{not json}", HostId);

        Assert.Empty(state);
        Assert.Single(_warnings);
    }

    [Fact]
    public void Deserialize_FutureSchema_DefaultsToNoUpgradesAndWarns()
    {
        var payload = new JObject
        {
            ["SchemaVersion"] = 99,
            ["SpeedPurchased"] = true,
            ["EnergyPurchased"] = true,
        };

        var state = _serializer.Deserialize(payload.ToString(), HostId);

        Assert.Empty(state);
        Assert.Single(_warnings);
    }
}
