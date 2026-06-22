namespace Dayswork.Tests.Persistence;

using Dayswork.Core.Persistence;
using Dayswork.Core.Upgrades;
using Newtonsoft.Json.Linq;
using Xunit;

public sealed class FarmhandUpgradeSaveDataSerializerTests
{
    private readonly List<string> _warnings = new();
    private readonly FarmhandUpgradeSaveDataSerializer _serializer;

    public FarmhandUpgradeSaveDataSerializerTests()
    {
        _serializer = new FarmhandUpgradeSaveDataSerializer(_warnings.Add);
    }

    [Fact]
    public void Deserialize_MissingPayload_DefaultsToNoUpgrades()
    {
        var state = _serializer.Deserialize(null);

        Assert.Equal(FarmhandUpgradeState.Empty, state);
        Assert.Empty(_warnings);
    }

    [Fact]
    public void SerializeAndDeserialize_PurchasedUpgrades_RoundTrip()
    {
        var state = new FarmhandUpgradeState(SpeedPurchased: true, EnergyPurchased: true);

        var hydrated = _serializer.Deserialize(_serializer.Serialize(state));

        Assert.Equal(state, hydrated);
    }

    [Fact]
    public void Deserialize_MalformedPayload_DefaultsToNoUpgradesAndWarns()
    {
        var state = _serializer.Deserialize("{not json}");

        Assert.Equal(FarmhandUpgradeState.Empty, state);
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

        var state = _serializer.Deserialize(payload.ToString());

        Assert.Equal(FarmhandUpgradeState.Empty, state);
        Assert.Single(_warnings);
    }
}
