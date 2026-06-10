using Dayswork.Core.Domain;
using Dayswork.Core.Persistence;
using Dayswork.Core.Persistence.Dto;
using Dayswork.Tests.Persistence.Generators;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Dayswork.Tests.Persistence;

public sealed class SaveDataSerializerTests
{
    private readonly List<string> _warnings = new();
    private readonly SaveDataSerializer _serializer;

    public SaveDataSerializerTests()
    {
        _serializer = new SaveDataSerializer(message => _warnings.Add(message));
    }

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
    public void Deserialize_NullPayload_ReturnsEmptyAndWarns()
    {
        var result = _serializer.Deserialize("null");
        Assert.Empty(result);
        Assert.Single(_warnings);
    }

    [Fact]
    public void Deserialize_FutureSchemaVersion_ReturnsEmptyAndWarns()
    {
        var result = _serializer.Deserialize(@"{""SchemaVersion"":4,""ModVersion"":""9.9.9"",""Contracts"":[]}");
        Assert.Empty(result);
        Assert.Single(_warnings);
    }

    [Fact]
    public void Deserialize_MalformedCurrentSchemaContract_SkipsItAndWarns()
    {
        var json = @"{
  ""SchemaVersion"": 3,
  ""ModVersion"": ""0.2.0"",
  ""Contracts"": [
    {
      ""Id"": ""not-a-guid"",
      ""EnabledTasks"": [""ClearWeeds""],
      ""TaskDestinations"": { ""ClearWeeds"": { ""Type"": ""ShippingBin"" } },
      ""Schedule"": ""OneTime"",
      ""Status"": ""Active"",
      ""HireDate"": { ""Day"": 1, ""Season"": ""Spring"", ""Year"": 1 },
      ""ScopeSelection"": {
        ""OutdoorZones"": [ { ""LocationName"": ""Farm"", ""TopLeftX"": 0, ""TopLeftY"": 0, ""BottomRightX"": 9, ""BottomRightY"": 9 } ],
        ""AnimalBuildings"": []
      },
      ""TermsSnapshot"": {
        ""Pricing"": { ""TotalPrice"": 100 },
        ""Energy"": {
          ""DailyCapacity"": 200,
          ""ActionCosts"": { ""ScytheSwing"": 1 }
        }
      },
      ""Tier"": ""FullDay"",
      ""CategoryPriority"": [ ""AnimalCare"", ""Crops"", ""Fieldwork"" ]
    }
  ]
}";

        var result = _serializer.Deserialize(json);

        Assert.Empty(result);
        Assert.Single(_warnings);
        Assert.Contains("Skipping schema v3 contract", _warnings[0]);
    }

    [Fact]
    public void Deserialize_MalformedCurrentSchemaContractAmongValid_PreservesValidSibling()
    {
        var validContract = PersistenceGenerators.CreateExampleCurrentSchemaContract();
        var validEnvelope = JObject.Parse(_serializer.Serialize(new[] { validContract }, "0.2.0"));
        var contracts = (JArray)validEnvelope["Contracts"]!;

        contracts.Add(new JObject
        {
            ["Id"] = "not-a-guid",
            ["EnabledTasks"] = new JArray(TaskKind.ClearWeeds.ToString()),
            ["TaskDestinations"] = new JObject
            {
                [TaskKind.ClearWeeds.ToString()] = new JObject { ["Type"] = "ShippingBin" },
            },
            ["Schedule"] = ContractSchedule.OneTime.ToString(),
            ["Status"] = ContractStatus.Active.ToString(),
            ["HireDate"] = new JObject
            {
                ["Day"] = 1,
                ["Season"] = Season.Spring.ToString(),
                ["Year"] = 1,
            },
            ["ScopeSelection"] = new JObject
            {
                ["OutdoorZones"] = new JArray(),
                ["AnimalBuildings"] = new JArray(),
            },
        });

        var result = _serializer.Deserialize(validEnvelope.ToString(Formatting.None));

        Assert.Single(result);
        Assert.True(ContractStructuralComparer.ContractsEqual(validContract, result[0]));
        Assert.Single(_warnings);
    }

    [Fact]
    public void Deserialize_CurrentSchemaContract_RoundTrips()
    {
        var contract = PersistenceGenerators.CreateExampleCurrentSchemaContract();

        var result = _serializer.Deserialize(_serializer.Serialize(new[] { contract }, "0.2.0"));

        var hydrated = Assert.Single(result);
        Assert.True(ContractStructuralComparer.ContractsEqual(contract, hydrated));
    }

    [Fact]
    public void Serialize_ProducesSchemaVersion3()
    {
        var json = _serializer.Serialize(Array.Empty<Contract>(), "0.2.0");
        Assert.Contains(@"""SchemaVersion"": 3", json);
    }

    [Fact]
    public void Serialize_CurrentSchemaContract_IncludesAuthoritativeScopeAndTerms()
    {
        var contract = PersistenceGenerators.CreateExampleCurrentSchemaContract();

        var payload = JObject.Parse(_serializer.Serialize(new[] { contract }, "0.2.0"));
        var dto = payload["Contracts"]!.Single()!;

        Assert.NotNull(dto["ScopeSelection"]);
        Assert.NotNull(dto["TermsSnapshot"]);
    }

    [Fact]
    public void Serialize_EmptyCropPlan_OmitsCropPlan()
    {
        var contract = PersistenceGenerators.CreateExampleCurrentSchemaContract();

        var payload = JObject.Parse(_serializer.Serialize(new[] { contract }, "0.2.0"));
        var dto = payload["Contracts"]!.Single()!;

        Assert.Null(dto["CropPlan"]);
    }

    [Fact]
    public void Deserialize_MissingCropPlan_DefaultsToEmpty()
    {
        var contract = PersistenceGenerators.CreateExampleCurrentSchemaContract();
        var payload = JObject.Parse(_serializer.Serialize(new[] { contract }, "0.2.0"));
        ((JObject)payload["Contracts"]!.Single()!).Remove("CropPlan");

        var result = _serializer.Deserialize(payload.ToString(Formatting.None));

        var hydrated = Assert.Single(result);
        Assert.False(hydrated.CropPlan.IsEnabled);
    }

    [Fact]
    public void Deserialize_MalformedCropPlan_SkipsOnlyAffectedContract()
    {
        var validContract = PersistenceGenerators.CreateExampleCurrentSchemaContract();
        var payload = JObject.Parse(_serializer.Serialize(new[] { validContract }, "0.2.0"));
        var malformed = (JObject)((JObject)payload["Contracts"]!.Single()!).DeepClone();
        malformed["Id"] = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee";
        malformed["CropPlan"] = new JObject
        {
            ["Assignments"] = new JArray
            {
                new JObject
                {
                    ["LocationName"] = "Farm",
                    ["Mode"] = "NotARealMode",
                    ["Choices"] = new JArray(),
                },
            },
        };
        ((JArray)payload["Contracts"]!).Add(malformed);

        var result = _serializer.Deserialize(payload.ToString(Formatting.None));

        var hydrated = Assert.Single(result);
        Assert.True(ContractStructuralComparer.ContractsEqual(validContract, hydrated));
        Assert.Single(_warnings);
        Assert.Contains("Skipping schema v3 contract", _warnings[0]);
    }
}
