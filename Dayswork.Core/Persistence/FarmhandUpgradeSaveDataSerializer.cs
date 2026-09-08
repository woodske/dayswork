using Dayswork.Core.Persistence.Dto;
using Dayswork.Core.Upgrades;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Dayswork.Core.Persistence;

public sealed class FarmhandUpgradeSaveDataSerializer
{
    private const int CurrentSchemaVersion = 3;

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

    public string Serialize(IReadOnlyDictionary<long, FarmhandUpgradeState> byOwner)
    {
        var dto = new FarmhandUpgradeSaveDataV1
        {
            SchemaVersion = CurrentSchemaVersion,
            // Ordered so the save file is stable diff-to-diff rather than dictionary-ordered.
            Owners = byOwner
                .OrderBy(entry => entry.Key)
                .ToDictionary(
                    entry => entry.Key.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    entry => new FarmhandUpgradeOwnerDto
                    {
                        SpeedPurchased = entry.Value.SpeedPurchased,
                        EnergyPurchased = entry.Value.EnergyPurchased,
                        Speed2Purchased = entry.Value.Speed2Purchased,
                    }),
        };

        return JsonConvert.SerializeObject(dto, SerializerSettings);
    }

    /// <summary>
    /// Reads the upgrade save data. A schema 1 or 2 payload held one global set of upgrades, which
    /// in a 1.x save can only have been bought by the host — so it migrates onto
    /// <paramref name="hostId"/>.
    /// </summary>
    public IReadOnlyDictionary<long, FarmhandUpgradeState> Deserialize(string? json, long hostId)
    {
        var empty = new Dictionary<long, FarmhandUpgradeState>();
        if (string.IsNullOrWhiteSpace(json))
            return empty;

        JObject payload;
        try
        {
            payload = JObject.Parse(json);
        }
        catch (JsonException ex)
        {
            _logWarning($"Dayswork upgrade save data could not be parsed — starting with no upgrades. ({ex.Message})");
            return empty;
        }

        var schemaToken = payload["SchemaVersion"];
        if (schemaToken?.Type != JTokenType.Integer)
        {
            _logWarning("Dayswork upgrade save data is missing a valid SchemaVersion — starting with no upgrades.");
            return empty;
        }

        var schemaVersion = schemaToken.Value<int>();
        if (schemaVersion > CurrentSchemaVersion)
        {
            _logWarning($"Dayswork upgrade save data schema version {schemaVersion} is newer than this mod supports (v{CurrentSchemaVersion}) — upgrades not loaded.");
            return empty;
        }

        if (schemaVersion < 1)
        {
            _logWarning($"Dayswork upgrade save data schema version {schemaVersion} is invalid for this mod version — starting with no upgrades.");
            return empty;
        }

        FarmhandUpgradeSaveDataV1? dto;
        try
        {
            dto = payload.ToObject<FarmhandUpgradeSaveDataV1>(JsonSerializer.Create(SerializerSettings));
        }
        catch (JsonException ex)
        {
            _logWarning($"Dayswork upgrade save data payload could not be mapped — starting with no upgrades. ({ex.Message})");
            return empty;
        }

        if (dto is null)
            return empty;

        if (schemaVersion >= 3)
            return ReadPerOwner(dto);

        // Schema v1 has no Speed2Purchased field — it deserializes to false, which is the correct
        // migration value (a v1 save never owned the second speed upgrade). Schemas 1 and 2 are
        // both global, and the only player who could have bought them is the host.
        var legacy = new FarmhandUpgradeState(dto.SpeedPurchased, dto.EnergyPurchased, dto.Speed2Purchased);
        return legacy == FarmhandUpgradeState.Empty
            ? empty
            : new Dictionary<long, FarmhandUpgradeState> { [hostId] = legacy };
    }

    private Dictionary<long, FarmhandUpgradeState> ReadPerOwner(FarmhandUpgradeSaveDataV1 dto)
    {
        var result = new Dictionary<long, FarmhandUpgradeState>();
        if (dto.Owners is null)
            return result;

        foreach (var (rawId, owner) in dto.Owners)
        {
            if (!long.TryParse(rawId, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var ownerId))
            {
                _logWarning($"Dayswork upgrade save data has an entry under a non-numeric player id '{rawId}' — skipping it.");
                continue;
            }

            result[ownerId] = new FarmhandUpgradeState(
                owner.SpeedPurchased,
                owner.EnergyPurchased,
                owner.Speed2Purchased);
        }

        return result;
    }
}
