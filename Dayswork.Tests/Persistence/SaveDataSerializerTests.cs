using Dayswork.Core.Domain;
using Dayswork.Core.Persistence;
using Dayswork.Tests.Persistence.Generators;
using FsCheck;
using FsCheck.Xunit;
using Xunit;

namespace Dayswork.Tests.Persistence;

public sealed class SaveDataSerializerTests
{
    private readonly List<string> _warnings = new List<string>();
    private readonly SaveDataSerializer _serializer;

    public SaveDataSerializerTests()
    {
        _serializer = new SaveDataSerializer(msg => _warnings.Add(msg));
    }

    // ── helpers ────────────────────────────────────────────────────────────

    private static bool ContractsEqual(Contract a, Contract b) =>
        a.Id == b.Id &&
        a.EnabledTasks.SetEquals(b.EnabledTasks) &&
        a.Zones.SequenceEqual(b.Zones) &&
        a.TaskDestinations.Count == b.TaskDestinations.Count &&
        a.TaskDestinations.All(kvp => b.TaskDestinations.TryGetValue(kvp.Key, out var v) && kvp.Value == v) &&
        a.Schedule == b.Schedule &&
        a.Status == b.Status &&
        a.HireDate == b.HireDate &&
        a.DepositAmount == b.DepositAmount &&
        a.HourlyRate == b.HourlyRate;

    // ── Deserialize: NFR-SAFE-03 edge cases ───────────────────────────────

    [Fact]
    public void Deserialize_Null_ReturnsEmpty()
    {
        var result = _serializer.Deserialize(null);
        Assert.Empty(result);
        Assert.Empty(_warnings);
    }

    [Fact]
    public void Deserialize_EmptyString_ReturnsEmpty()
    {
        var result = _serializer.Deserialize("");
        Assert.Empty(result);
        Assert.Empty(_warnings);
    }

    [Fact]
    public void Deserialize_InvalidJson_ReturnsEmptyAndWarns()
    {
        var result = _serializer.Deserialize("{not valid json}");
        Assert.Empty(result);
        Assert.Single(_warnings);
    }

    [Fact]
    public void Deserialize_NullEnvelope_ReturnsEmptyAndWarns()
    {
        var result = _serializer.Deserialize("null");
        Assert.Empty(result);
        Assert.Single(_warnings);
    }

    [Fact]
    public void Deserialize_FutureSchemaVersion_ReturnsEmptyAndWarns()
    {
        var json = @"{""SchemaVersion"":2,""ModVersion"":""9.9.9"",""Contracts"":[]}";
        var result = _serializer.Deserialize(json);
        Assert.Empty(result);
        Assert.Single(_warnings);
    }

    [Fact]
    public void Deserialize_MalformedContract_SkipsItAndWarns()
    {
        var json = @"{
  ""SchemaVersion"": 1,
  ""ModVersion"": ""0.1.0"",
  ""Contracts"": [
    {
      ""Id"": ""not-a-guid"",
      ""EnabledTasks"": [],
      ""Zones"": [],
      ""TaskDestinations"": {},
      ""Schedule"": ""OneTime"",
      ""Status"": ""Active"",
      ""HireDate"": { ""Day"": 1, ""Season"": ""Spring"", ""Year"": 1 },
      ""DepositAmount"": 0,
      ""HourlyRate"": 70
    }
  ]
}";
        var result = _serializer.Deserialize(json);
        Assert.Empty(result);
        Assert.Single(_warnings);
    }

    [Fact]
    public void Deserialize_MalformedContractAmongValid_ReturnsOnlyValidOnes()
    {
        var validId = Guid.NewGuid();
        var json = $@"{{
  ""SchemaVersion"": 1,
  ""ModVersion"": ""0.1.0"",
  ""Contracts"": [
    {{
      ""Id"": ""{validId}"",
      ""EnabledTasks"": [""ClearWeeds""],
      ""Zones"": [{{ ""LocationName"": ""Farm"", ""TopLeftX"": 0, ""TopLeftY"": 0, ""BottomRightX"": 9, ""BottomRightY"": 9 }}],
      ""TaskDestinations"": {{ ""ClearWeeds"": {{ ""Type"": ""ShippingBin"" }} }},
      ""Schedule"": ""OneTime"",
      ""Status"": ""Active"",
      ""HireDate"": {{ ""Day"": 1, ""Season"": ""Spring"", ""Year"": 1 }},
      ""DepositAmount"": 100,
      ""HourlyRate"": 70
    }},
    {{
      ""Id"": ""not-a-guid"",
      ""EnabledTasks"": [],
      ""Zones"": [],
      ""TaskDestinations"": {{}},
      ""Schedule"": ""OneTime"",
      ""Status"": ""Active"",
      ""HireDate"": {{ ""Day"": 1, ""Season"": ""Spring"", ""Year"": 1 }},
      ""DepositAmount"": 0,
      ""HourlyRate"": 70
    }}
  ]
}}";
        var result = _serializer.Deserialize(json);
        Assert.Single(result);
        Assert.Equal(new ContractId(validId), result[0].Id);
        Assert.Single(_warnings);
    }

    [Fact]
    public void Deserialize_UnknownDestinationType_SkipsContractAndWarns()
    {
        var id = Guid.NewGuid();
        var json = $@"{{
  ""SchemaVersion"": 1,
  ""ModVersion"": ""0.1.0"",
  ""Contracts"": [
    {{
      ""Id"": ""{id}"",
      ""EnabledTasks"": [""ClearWeeds""],
      ""Zones"": [{{ ""LocationName"": ""Farm"", ""TopLeftX"": 0, ""TopLeftY"": 0, ""BottomRightX"": 9, ""BottomRightY"": 9 }}],
      ""TaskDestinations"": {{ ""ClearWeeds"": {{ ""Type"": ""Teleporter"" }} }},
      ""Schedule"": ""OneTime"",
      ""Status"": ""Active"",
      ""HireDate"": {{ ""Day"": 1, ""Season"": ""Spring"", ""Year"": 1 }},
      ""DepositAmount"": 100,
      ""HourlyRate"": 70
    }}
  ]
}}";
        var result = _serializer.Deserialize(json);
        Assert.Empty(result);
        Assert.Single(_warnings);
    }

    // ── Serialize ──────────────────────────────────────────────────────────

    [Fact]
    public void Serialize_ProducesValidJson_ContainingSchemaVersion1()
    {
        var json = _serializer.Serialize(new List<Contract>(), "0.1.0");
        Assert.Contains("\"SchemaVersion\": 1", json);
    }

    [Fact]
    public void Serialize_ModVersionPresentInOutput()
    {
        var json = _serializer.Serialize(new List<Contract>(), "1.2.3");
        Assert.Contains("\"ModVersion\": \"1.2.3\"", json);
    }

    // ── PBT-02: Round-trip ─────────────────────────────────────────────────

    [Property(Arbitrary = new[] { typeof(ContractGen) }, MaxTest = 1000)]
    public bool RoundTrip_DeserializeSerialize_IsIdentity(Contract contract)
    {
        var original = new List<Contract> { contract }.AsReadOnly();
        var json = _serializer.Serialize(original, "0.1.0");
        var deserialized = _serializer.Deserialize(json);
        return deserialized.Count == 1 && ContractsEqual(contract, deserialized[0]);
    }
}
