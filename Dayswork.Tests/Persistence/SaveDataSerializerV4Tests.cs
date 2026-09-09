using Dayswork.Core.Domain;
using Dayswork.Core.Persistence;
using Dayswork.Tests.Persistence.Generators;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Dayswork.Tests.Persistence;

/// <summary>
/// Schema v4: contracts carry an owner, an office, and a revision, and they are stored one per
/// office in that office's <c>Building.modData</c> rather than as a flat list in the save key.
/// </summary>
public sealed class SaveDataSerializerV4Tests
{
    private readonly List<string> _warnings = new();
    private readonly SaveDataSerializer _serializer;

    public SaveDataSerializerV4Tests()
    {
        _serializer = new SaveDataSerializer(message => _warnings.Add(message));
    }

    private static Contract Bound() =>
        PersistenceGenerators.CreateExampleCurrentSchemaContract() with
        {
            OwnerId = 76561190000000123L,
            OfficeId = Guid.Parse("0a0a0a0a-1111-2222-3333-444444444444"),
            Revision = 9,
        };

    // ── Round trip ───────────────────────────────────────────────────────────

    [Fact]
    public void Owner_office_and_revision_round_trip()
    {
        var contract = Bound();

        var hydrated = Assert.Single(_serializer.Deserialize(_serializer.Serialize(new[] { contract }, "2.0.0")));

        Assert.Equal(contract.OwnerId, hydrated.OwnerId);
        Assert.Equal(contract.OfficeId, hydrated.OfficeId);
        Assert.Equal(contract.Revision, hydrated.Revision);
        Assert.True(ContractStructuralComparer.ContractsEqual(contract, hydrated));
    }

    [Fact]
    public void Owner_preferences_round_trip()
    {
        var contract = Bound() with
        {
            Preferences = new ContractPreferences(
                AvoidBlueGrass: false,
                IdleTask: IdleTaskKind.None,
                WorkerName: "Rosa",
                RunWhileOwnerOffline: false,
                GrantExperience: false,
                Appearance: "green"),
        };

        var hydrated = Assert.Single(_serializer.Deserialize(_serializer.Serialize(new[] { contract }, "2.0.0")));

        Assert.Equal(contract.Preferences, hydrated.Preferences);
    }

    // ── v3 → v4 upgrade ──────────────────────────────────────────────────────

    [Fact]
    public void V3_contract_loads_unbound_so_adoption_can_bind_it()
    {
        var payload = JObject.Parse(_serializer.Serialize(new[] { Bound() }, "2.0.0"));
        payload["SchemaVersion"] = 3;
        var dto = (JObject)payload["Contracts"]!.Single()!;
        dto.Remove("OwnerId");
        dto.Remove("OfficeId");
        dto.Remove("Revision");

        var hydrated = Assert.Single(_serializer.Deserialize(payload.ToString(Formatting.None)));

        Assert.Equal(0L, hydrated.OwnerId);
        Assert.Equal(Guid.Empty, hydrated.OfficeId);
        Assert.Equal(0, hydrated.Revision);
        Assert.Empty(_warnings);
    }

    [Fact]
    public void V3_preferences_take_the_new_defaults_rather_than_false()
    {
        var payload = JObject.Parse(_serializer.Serialize(new[] { Bound() }, "2.0.0"));
        payload["SchemaVersion"] = 3;
        var preferences = (JObject)payload["Contracts"]!.Single()!["Preferences"]!;
        preferences.Remove("RunWhileOwnerOffline");
        preferences.Remove("GrantExperience");
        preferences.Remove("Appearance");

        var hydrated = Assert.Single(_serializer.Deserialize(payload.ToString(Formatting.None)));

        Assert.True(hydrated.Preferences.RunWhileOwnerOffline);
        Assert.True(hydrated.Preferences.GrantExperience);
        Assert.Equal("", hydrated.Preferences.Appearance);
    }

    [Fact]
    public void MalformedOfficeId_SkipsOnlyThatContract()
    {
        // A garbled office id must not silently bind the contract to the wrong building.
        var payload = JObject.Parse(_serializer.Serialize(new[] { Bound() }, "2.0.0"));
        ((JObject)payload["Contracts"]!.Single()!)["OfficeId"] = "not-a-guid";

        Assert.Empty(_serializer.Deserialize(payload.ToString(Formatting.None)));
        Assert.Single(_warnings);
    }

    // ── Per-office storage ───────────────────────────────────────────────────

    [Fact]
    public void SerializeOne_and_DeserializeOne_round_trip_one_offices_contract()
    {
        var contract = Bound();

        var hydrated = _serializer.DeserializeOne(_serializer.SerializeOne(contract, "2.0.0"));

        Assert.NotNull(hydrated);
        Assert.True(ContractStructuralComparer.ContractsEqual(contract, hydrated!));
    }

    [Fact]
    public void Dayswork2Review_R5_DeferredPrepaidContractRemainsDueAfterSaveReload()
    {
        var contract = Bound() with
        {
            Schedule = ContractSchedule.OneTime,
            Status = ContractStatus.Active,
            HireDate = new GameDate(28, Season.Fall, 1),
        };
        var hydrated = _serializer.DeserializeOne(_serializer.SerializeOne(contract, "2.0.0-beta.1"));
        var store = new OfficeContractStore(_warnings.Add);
        store.HydrateOffice(contract.OfficeId, hydrated);

        Assert.Single(store.ScheduledForDate(1, Season.Winter, 1));
        Assert.Single(store.ScheduledForDate(1, Season.Spring, 2));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("{not valid json}")]
    public void DeserializeOne_AbsentOrMalformed_ReturnsNull(string? json)
    {
        Assert.Null(_serializer.DeserializeOne(json));
    }

    [Fact]
    public void DeserializeOne_TakesTheFirstAndWarns_WhenAnOfficeSomehowHoldsSeveral()
    {
        var json = _serializer.Serialize(new[] { Bound(), Bound() with { Id = ContractId.New() } }, "2.0.0");

        Assert.NotNull(_serializer.DeserializeOne(json));
        Assert.Single(_warnings);
    }

    // ── Legacy-key migration marker ──────────────────────────────────────────

    [Fact]
    public void MigrationMarker_IsCurrentSchema_AndHoldsNoContracts()
    {
        var marker = JObject.Parse(_serializer.SerializeMigrationMarker("2.0.0"));

        Assert.Equal(4, marker["SchemaVersion"]!.Value<int>());
        Assert.True(marker["MigratedToBuildingModData"]!.Value<bool>());
        Assert.Empty(_serializer.Deserialize(marker.ToString(Formatting.None)));
    }

    [Fact]
    public void IsMigrationMarker_RecognisesTheMarkerAndNothingElse()
    {
        Assert.True(_serializer.IsMigrationMarker(_serializer.SerializeMigrationMarker("2.0.0")));
        Assert.False(_serializer.IsMigrationMarker(_serializer.Serialize(new[] { Bound() }, "2.0.0")));
        Assert.False(_serializer.IsMigrationMarker(null));
        Assert.False(_serializer.IsMigrationMarker("{not valid json}"));
    }
}
