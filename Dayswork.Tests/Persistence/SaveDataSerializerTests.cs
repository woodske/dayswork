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
    public void Deserialize_LegacySchemaV1_ReturnsEmptyAndWarns()
    {
        var legacyEnvelope = new DaysworkSaveDataV1
        {
            SchemaVersion = 1,
            ModVersion = "0.1.0",
            Contracts = new List<ContractDtoV1>
            {
                new()
                {
                    Id = Guid.NewGuid().ToString(),
                    EnabledTasks = new List<string> { TaskKind.ClearWeeds.ToString() },
                    Zones = new List<ZoneDtoV1> { new() { LocationName = "Farm", TopLeftX = 0, TopLeftY = 0, BottomRightX = 9, BottomRightY = 9 } },
                    TaskDestinations = new Dictionary<string, DestinationDtoV1>
                    {
                        [TaskKind.ClearWeeds.ToString()] = new() { Type = "ShippingBin" },
                    },
                    Schedule = ContractSchedule.OneTime.ToString(),
                    Status = ContractStatus.Active.ToString(),
                    HireDate = new GameDateDtoV1 { Day = 1, Season = Season.Spring.ToString(), Year = 1 },
                    DepositAmount = 100,
                    HourlyRate = 70,
                },
            },
        };

        var result = _serializer.Deserialize(JsonConvert.SerializeObject(legacyEnvelope));

        Assert.Empty(result);
        Assert.Single(_warnings);
        Assert.Contains("legacy pre-release hourly contract data", _warnings[0]);
    }

    [Fact]
    public void Deserialize_FutureSchemaVersion_ReturnsEmptyAndWarns()
    {
        var result = _serializer.Deserialize(@"{""SchemaVersion"":3,""ModVersion"":""9.9.9"",""Contracts"":[]}");
        Assert.Empty(result);
        Assert.Single(_warnings);
    }

    [Fact]
    public void Deserialize_MalformedCurrentSchemaContract_SkipsItAndWarns()
    {
        var json = @"{
  ""SchemaVersion"": 2,
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
        ""Pricing"": {
          ""LineItems"": [],
          ""OutdoorSubtotal"": 0,
          ""AnimalSubtotal"": 0,
          ""GreenhouseSubtotal"": 0,
          ""TotalPrice"": 100
        },
        ""Energy"": {
          ""DailyCapacity"": 270,
          ""ActionCosts"": { ""ScytheSwing"": 1 }
        }
      },
      ""LegacyFinancialBridge"": { ""DepositAmount"": 100, ""HourlyRate"": 70 }
    }
  ]
}";

        var result = _serializer.Deserialize(json);

        Assert.Empty(result);
        Assert.Single(_warnings);
        Assert.Contains("Skipping schema v2 contract", _warnings[0]);
    }

    [Fact]
    public void Deserialize_MalformedCurrentSchemaContractAmongValid_PreservesValidSibling()
    {
        var validContract = U19PersistenceGen.CreateExampleCurrentSchemaContract();
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
            ["LegacyFinancialBridge"] = new JObject
            {
                ["DepositAmount"] = 100,
                ["HourlyRate"] = 70,
            },
        });

        var result = _serializer.Deserialize(validEnvelope.ToString(Formatting.None));

        Assert.Single(result);
        Assert.True(ContractStructuralComparer.ContractsEqual(validContract, result[0]));
        Assert.Single(_warnings);
    }

    [Fact]
    public void Deserialize_CurrentSchemaContract_ProjectsCompatibilityFields()
    {
        var contract = U19PersistenceGen.CreateExampleCurrentSchemaContract();

        var result = _serializer.Deserialize(_serializer.Serialize(new[] { contract }, "0.2.0"));

        var hydrated = Assert.Single(result);
        Assert.True(ContractStructuralComparer.ContractsEqual(contract, hydrated));
        Assert.Equal(contract.Zones, hydrated.Zones);
        Assert.Equal(contract.DepositAmount, hydrated.DepositAmount);
        Assert.Equal(contract.HourlyRate, hydrated.HourlyRate);
    }

    [Fact]
    public void Serialize_ProducesSchemaVersion2()
    {
        var json = _serializer.Serialize(Array.Empty<Contract>(), "0.2.0");
        Assert.Contains(@"""SchemaVersion"": 2", json);
    }

    [Fact]
    public void Serialize_CurrentSchemaContract_IncludesAuthoritativeScopeAndTerms()
    {
        var contract = U19PersistenceGen.CreateExampleCurrentSchemaContract();

        var payload = JObject.Parse(_serializer.Serialize(new[] { contract }, "0.2.0"));
        var dto = payload["Contracts"]!.Single()!;

        Assert.NotNull(dto["ScopeSelection"]);
        Assert.NotNull(dto["TermsSnapshot"]);
        Assert.NotNull(dto["LegacyFinancialBridge"]);
    }
}
