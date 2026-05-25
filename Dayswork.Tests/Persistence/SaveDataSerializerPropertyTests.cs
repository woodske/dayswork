using Dayswork.Core.Domain;
using Dayswork.Core.Persistence;
using Dayswork.Tests.Persistence.Generators;
using FsCheck;
using FsCheck.Xunit;
using Newtonsoft.Json.Linq;

namespace Dayswork.Tests.Persistence;

public sealed class SaveDataSerializerPropertyTests
{
    private static SaveDataSerializer CreateSerializer() => new(_ => { });

    [Property(Arbitrary = new[] { typeof(U19PersistenceGen) }, MaxTest = 300)]
    public bool RoundTrip_CurrentSchemaContract_IsIdentity(Contract contract)
    {
        var serializer = CreateSerializer();
        var json = serializer.Serialize(new[] { contract }, "0.2.0");
        var deserialized = serializer.Deserialize(json);
        return deserialized.Count == 1 && ContractStructuralComparer.ContractsEqual(contract, deserialized[0]);
    }

    [Property(Arbitrary = new[] { typeof(U19PersistenceGen) }, MaxTest = 300)]
    public bool Serialize_Twice_IsDeterministic(Contract contract)
    {
        var serializer = CreateSerializer();
        var first = serializer.Serialize(new[] { contract }, "0.2.0");
        var second = serializer.Serialize(new[] { contract }, "0.2.0");
        return first == second;
    }

    [Property(Arbitrary = new[] { typeof(U19PersistenceGen) }, MaxTest = 300)]
    public Property Serialize_ContractOrderingIsCanonical(IReadOnlyList<Contract> contracts)
    {
        if (contracts.Count == 0)
            return true.ToProperty();

        var serializer = CreateSerializer();
        var reversed = contracts.Reverse().ToList();
        var first = serializer.Serialize(contracts, "0.2.0");
        var second = serializer.Serialize(reversed, "0.2.0");
        return (first == second).ToProperty()
            .Label($"contracts={string.Join("|", contracts.Select(ContractStructuralComparer.DescribeContract))}");
    }

    [Property(Arbitrary = new[] { typeof(U19PersistenceGen) }, MaxTest = 300)]
    public Property MalformedV2Sibling_DoesNotPoisonValidContract(Contract contract)
    {
        var warnings = new List<string>();
        var serializer = new SaveDataSerializer(message => warnings.Add(message));
        var payload = JObject.Parse(serializer.Serialize(new[] { contract }, "0.2.0"));

        ((JArray)payload["Contracts"]!).Add(new JObject
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
            ["TermsSnapshot"] = new JObject
            {
                ["Pricing"] = new JObject
                {
                    ["LineItems"] = new JArray(),
                    ["OutdoorSubtotal"] = 0,
                    ["AnimalSubtotal"] = 0,
                    ["GreenhouseSubtotal"] = 0,
                    ["TotalPrice"] = 0,
                },
                ["Energy"] = new JObject
                {
                    ["DailyCapacity"] = 270,
                    ["ActionCosts"] = new JObject(),
                },
            },
        });

        var result = serializer.Deserialize(payload.ToString());

        return (result.Count == 1
                && ContractStructuralComparer.ContractsEqual(contract, result[0])
                && warnings.Count == 1)
            .ToProperty()
            .Label(ContractStructuralComparer.DescribeContract(contract));
    }
}
