using Dayswork.Core.Persistence.Dto;
using Dayswork.Core.Upgrades;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Dayswork.Core.Persistence;

public sealed class FarmhandUpgradeSaveDataSerializer
{
    private const int CurrentSchemaVersion = 2;

    private static readonly JsonSerializerSettings SerializerSettings = new()
    {
        Formatting = Formatting.Indented,
        NullValueHandling = NullValueHandling.Ignore,
    };

    private readonly Action<string> _logWarning;

    public FarmhandUpgradeSaveDataSerializer(Action<string> logWarning)
    {
        _logWarning = logWarning;
    }

    public string Serialize(FarmhandUpgradeState state)
    {
        var dto = new FarmhandUpgradeSaveDataV1
        {
            SchemaVersion = CurrentSchemaVersion,
            SpeedPurchased = state.SpeedPurchased,
            EnergyPurchased = state.EnergyPurchased,
            Speed2Purchased = state.Speed2Purchased,
        };

        return JsonConvert.SerializeObject(dto, SerializerSettings);
    }

    public FarmhandUpgradeState Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return FarmhandUpgradeState.Empty;

        JObject payload;
        try
        {
            payload = JObject.Parse(json);
        }
        catch (JsonException ex)
        {
            _logWarning($"Dayswork upgrade save data could not be parsed — starting with no upgrades. ({ex.Message})");
            return FarmhandUpgradeState.Empty;
        }

        var schemaToken = payload["SchemaVersion"];
        if (schemaToken?.Type != JTokenType.Integer)
        {
            _logWarning("Dayswork upgrade save data is missing a valid SchemaVersion — starting with no upgrades.");
            return FarmhandUpgradeState.Empty;
        }

        var schemaVersion = schemaToken.Value<int>();
        if (schemaVersion > CurrentSchemaVersion)
        {
            _logWarning($"Dayswork upgrade save data schema version {schemaVersion} is newer than this mod supports (v{CurrentSchemaVersion}) — upgrades not loaded.");
            return FarmhandUpgradeState.Empty;
        }

        if (schemaVersion < 1)
        {
            _logWarning($"Dayswork upgrade save data schema version {schemaVersion} is invalid for this mod version — starting with no upgrades.");
            return FarmhandUpgradeState.Empty;
        }

        // Schema v1 has no Speed2Purchased field — it deserializes to false, which is the
        // correct migration value (a v1 save never owned the second speed upgrade).
        try
        {
            var dto = payload.ToObject<FarmhandUpgradeSaveDataV1>(JsonSerializer.Create(SerializerSettings));
            return dto is null
                ? FarmhandUpgradeState.Empty
                : new FarmhandUpgradeState(dto.SpeedPurchased, dto.EnergyPurchased, dto.Speed2Purchased);
        }
        catch (JsonException ex)
        {
            _logWarning($"Dayswork upgrade save data payload could not be mapped — starting with no upgrades. ({ex.Message})");
            return FarmhandUpgradeState.Empty;
        }
    }
}
