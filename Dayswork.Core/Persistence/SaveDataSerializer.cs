using Dayswork.Core.Config;
using Dayswork.Core.Domain;
using Dayswork.Core.Energy;
using Dayswork.Core.Persistence.Dto;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Dayswork.Core.Persistence;

public sealed class SaveDataSerializer : ISaveDataSerializer
{
    private const int CurrentSchemaVersion = 3;

    private static readonly JsonSerializerSettings SerializerSettings = new()
    {
        Formatting = Formatting.Indented,
        NullValueHandling = NullValueHandling.Ignore,
    };

    private readonly Action<string> _logWarning;

    public SaveDataSerializer(Action<string> logWarning)
    {
        _logWarning = logWarning;
    }

    public string Serialize(IReadOnlyList<Contract> contracts, string modVersion)
    {
        var envelope = new DaysworkSaveDataV2
        {
            SchemaVersion = CurrentSchemaVersion,
            ModVersion = modVersion,
            Contracts = contracts
                .OrderBy(contract => contract.Id.Value)
                .Select(MapDomainToDtoV2)
                .ToList(),
        };

        return JsonConvert.SerializeObject(envelope, SerializerSettings);
    }

    public IReadOnlyList<Contract> Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Array.Empty<Contract>();

        JToken payload;
        try
        {
            payload = JToken.Parse(json);
        }
        catch (JsonException ex)
        {
            _logWarning($"Dayswork save data could not be parsed — starting fresh. ({ex.Message})");
            return Array.Empty<Contract>();
        }

        if (payload.Type == JTokenType.Null)
        {
            _logWarning("Dayswork save data deserialized to null — starting fresh.");
            return Array.Empty<Contract>();
        }

        if (payload is not JObject envelopeObject)
        {
            _logWarning("Dayswork save data payload was not a JSON object — starting fresh.");
            return Array.Empty<Contract>();
        }

        var schemaVersion = ReadSchemaVersion(envelopeObject);
        if (schemaVersion is null)
            return Array.Empty<Contract>();

        if (schemaVersion.Value > CurrentSchemaVersion)
        {
            _logWarning($"Dayswork save data schema version {schemaVersion.Value} is newer than this mod supports (v{CurrentSchemaVersion}). Contracts not loaded — please update the mod.");
            return Array.Empty<Contract>();
        }

        if (schemaVersion.Value != CurrentSchemaVersion)
        {
            _logWarning($"Dayswork save data schema version {schemaVersion.Value} is invalid for this mod version — starting fresh.");
            return Array.Empty<Contract>();
        }

        DaysworkSaveDataV2? envelope;
        try
        {
            envelope = envelopeObject.ToObject<DaysworkSaveDataV2>(JsonSerializer.Create(SerializerSettings));
        }
        catch (JsonException ex)
        {
            _logWarning($"Dayswork save data schema v3 payload could not be mapped — starting fresh. ({ex.Message})");
            return Array.Empty<Contract>();
        }

        if (envelope is null)
        {
            _logWarning("Dayswork save data schema v2 payload mapped to null — starting fresh.");
            return Array.Empty<Contract>();
        }

        var results = new List<Contract>();
        foreach (var dto in envelope.Contracts ?? new List<ContractDtoV2>())
        {
            try
            {
                results.Add(MapDtoV2ToDomain(dto));
            }
            catch (Exception ex)
            {
                var contractId = string.IsNullOrWhiteSpace(dto?.Id) ? "<unknown>" : dto.Id;
                _logWarning($"Skipping schema v3 contract '{contractId}': {ex.Message}");
            }
        }

        return results.AsReadOnly();
    }

    private int? ReadSchemaVersion(JObject envelopeObject)
    {
        var schemaToken = envelopeObject["SchemaVersion"];
        if (schemaToken is null)
        {
            _logWarning("Dayswork save data payload is missing SchemaVersion — starting fresh.");
            return null;
        }

        if (schemaToken.Type != JTokenType.Integer)
        {
            _logWarning("Dayswork save data SchemaVersion was not an integer — starting fresh.");
            return null;
        }

        return schemaToken.Value<int>();
    }

    private static ContractDtoV2 MapDomainToDtoV2(Contract contract) =>
        new()
        {
            Id = contract.Id.Value.ToString(),
            EnabledTasks = contract.EnabledTasks
                .OrderBy(task => task.ToString(), StringComparer.Ordinal)
                .Select(task => task.ToString())
                .ToList(),
            TaskDestinations = contract.TaskDestinations
                .OrderBy(kvp => kvp.Key.ToString(), StringComparer.Ordinal)
                .ToDictionary(
                    kvp => kvp.Key.ToString(),
                    kvp => MapDestinationToDto(kvp.Value),
                    StringComparer.Ordinal)
                .ToSortedDictionary(StringComparer.Ordinal),
            Schedule = contract.Schedule.ToString(),
            Status = contract.Status.ToString(),
            HireDate = MapDate(contract.HireDate),
            ScopeSelection = MapScopeSelection(contract.ScopeSelection),
            TermsSnapshot = MapTermsSnapshot(contract.TermsSnapshot),
            Tier = contract.Tier.ToString(),
            CategoryPriority = contract.CategoryPriority.Select(category => category.ToString()).ToList(),
        };

    private static Contract MapDtoV2ToDomain(ContractDtoV2 dto)
    {
        var id = new ContractId(Guid.Parse(dto.Id));
        var enabledTasks = (dto.EnabledTasks ?? throw new JsonException("EnabledTasks was null."))
            .Select(value => Enum.Parse<TaskKind>(value))
            .ToHashSet();

        var destinations = (dto.TaskDestinations ?? throw new JsonException("TaskDestinations was null."))
            .ToDictionary(
                kvp => Enum.Parse<TaskKind>(kvp.Key),
                kvp => MapDestinationToDomain(kvp.Value));

        var scopeSelection = MapScopeSelection(dto.ScopeSelection ?? throw new JsonException("ScopeSelection was null."));
        var termsSnapshot = MapTermsSnapshot(dto.TermsSnapshot ?? throw new JsonException("TermsSnapshot was null."));

        var tier = string.IsNullOrWhiteSpace(dto.Tier)
            ? throw new JsonException("Tier was null or empty.")
            : Enum.Parse<EnergyTier>(dto.Tier);

        var categoryPriority = MapCategoryPriority(dto.CategoryPriority);

        return new Contract(
            Id: id,
            EnabledTasks: enabledTasks,
            TaskDestinations: destinations,
            Schedule: Enum.Parse<ContractSchedule>(dto.Schedule),
            Status: Enum.Parse<ContractStatus>(dto.Status),
            HireDate: MapDate(dto.HireDate ?? throw new JsonException("HireDate was null.")),
            ScopeSelection: scopeSelection,
            TermsSnapshot: termsSnapshot,
            Tier: tier,
            CategoryPriority: categoryPriority);
    }

    // Player category priority. Parse the saved order, dropping unknown/duplicate entries, then
    // append any categories the save omitted (in default order) so the result always covers every
    // TaskCategory deterministically.
    private static IReadOnlyList<TaskCategory> MapCategoryPriority(IReadOnlyList<string>? saved)
    {
        var ordered = new List<TaskCategory>();
        foreach (var name in saved ?? Array.Empty<string>())
        {
            if (Enum.TryParse<TaskCategory>(name, out var category) && !ordered.Contains(category))
                ordered.Add(category);
        }

        foreach (var category in TaskKindSets.DefaultCategoryPriority)
            if (!ordered.Contains(category))
                ordered.Add(category);

        return ordered.AsReadOnly();
    }

    private static ContractScopeSelectionDto MapScopeSelection(ContractScopeSelection selection) =>
        new()
        {
            OutdoorZones = selection.OutdoorZones
                .OrderBy(DescribeZone, StringComparer.Ordinal)
                .Select(MapZone)
                .ToList(),
            AnimalBuildings = selection.AnimalBuildings
                .OrderBy(building => building.LocationName, StringComparer.Ordinal)
                .ThenBy(building => building.Tier)
                .Select(building => new AnimalBuildingSelectionDto
                {
                    LocationName = building.LocationName,
                    Tier = building.Tier.ToString(),
                })
                .ToList(),
            Greenhouses = selection.Greenhouses
                .OrderBy(greenhouse => greenhouse.LocationName, StringComparer.Ordinal)
                .Select(greenhouse => new GreenhouseSelectionDto { LocationName = greenhouse.LocationName })
                .ToList(),
        };

    private static ContractScopeSelection MapScopeSelection(ContractScopeSelectionDto dto)
    {
        var outdoorZones = (dto.OutdoorZones ?? throw new JsonException("OutdoorZones was null."))
            .Select(MapZone)
            .OrderBy(DescribeZone, StringComparer.Ordinal)
            .ToList();

        var animalBuildings = (dto.AnimalBuildings ?? throw new JsonException("AnimalBuildings was null."))
            .Select(building => new AnimalBuildingSelection(
                building.LocationName,
                Enum.Parse<AnimalBuildingTier>(building.Tier)))
            .Distinct()
            .OrderBy(building => building.LocationName, StringComparer.Ordinal)
            .ThenBy(building => building.Tier)
            .ToList();

        var greenhouses = (dto.Greenhouses ?? new List<GreenhouseSelectionDto>())
            .Select(greenhouse => greenhouse.LocationName)
            .Where(locationName => !string.IsNullOrWhiteSpace(locationName))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(locationName => locationName, StringComparer.Ordinal)
            .Select(locationName => new GreenhouseSelection(locationName))
            .ToList();

        return new ContractScopeSelection(
            OutdoorZones: outdoorZones.AsReadOnly(),
            AnimalBuildings: animalBuildings.AsReadOnly(),
            Greenhouses: greenhouses.AsReadOnly());
    }

    private static ContractTermsSnapshotDto MapTermsSnapshot(ContractTermsSnapshot snapshot) =>
        new()
        {
            Pricing = new PricingSnapshotDto
            {
                TotalPrice = snapshot.Pricing.TotalPrice,
            },
            Energy = new WorkerEnergyProfileDto
            {
                DailyCapacity = snapshot.Energy.DailyCapacity,
                ActionCosts = snapshot.Energy.ActionCosts
                    .OrderBy(kvp => kvp.Key.ToString(), StringComparer.Ordinal)
                    .ToDictionary(
                        kvp => kvp.Key.ToString(),
                        kvp => kvp.Value,
                        StringComparer.Ordinal)
                    .ToSortedDictionary(StringComparer.Ordinal),
            },
        };

    private static ContractTermsSnapshot MapTermsSnapshot(ContractTermsSnapshotDto dto)
    {
        var pricingSnapshotDto = dto.Pricing ?? throw new JsonException("Pricing was null.");
        var pricing = new PricingSnapshot(pricingSnapshotDto.TotalPrice);

        var energyDto = dto.Energy ?? throw new JsonException("Energy was null.");
        var actionCosts = (energyDto.ActionCosts ?? throw new JsonException("Energy.ActionCosts was null."))
            .ToDictionary(
                kvp => Enum.Parse<WorkActionKind>(kvp.Key),
                kvp => kvp.Value);

        var energy = new WorkerEnergyProfile(energyDto.DailyCapacity, actionCosts);
        return new ContractTermsSnapshot(pricing, energy);
    }

    private static DestinationDtoV1 MapDestinationToDto(DestinationKey destination) =>
        destination switch
        {
            ChestDestination chest => new DestinationDtoV1
            {
                Type = "Chest",
                LocationName = chest.Ref.LocationName,
                X = chest.Ref.Tile.X,
                Y = chest.Ref.Tile.Y,
            },
            ShippingBinDestination => new DestinationDtoV1 { Type = "ShippingBin" },
            MailDestination => new DestinationDtoV1 { Type = "Mail" },
            _ => throw new JsonException($"Unknown DestinationKey type: {destination.GetType().Name}"),
        };

    private static DestinationKey MapDestinationToDomain(DestinationDtoV1 dto) =>
        dto.Type switch
        {
            "Chest" => new ChestDestination(new ChestRef(
                dto.LocationName ?? throw new JsonException("Chest destination missing LocationName."),
                new TileCoord(
                    dto.X ?? throw new JsonException("Chest destination missing X."),
                    dto.Y ?? throw new JsonException("Chest destination missing Y.")))),
            "ShippingBin" => ShippingBinDestination.Instance,
            "Mail" => MailDestination.Instance,
            _ => throw new JsonException($"Unknown destination type: '{dto.Type}'."),
        };

    private static ZoneDtoV1 MapZone(Zone zone) =>
        new()
        {
            LocationName = zone.LocationName,
            TopLeftX = zone.TopLeft.X,
            TopLeftY = zone.TopLeft.Y,
            BottomRightX = zone.BottomRight.X,
            BottomRightY = zone.BottomRight.Y,
        };

    private static Zone MapZone(ZoneDtoV1 dto) =>
        new(
            dto.LocationName,
            new TileCoord(dto.TopLeftX, dto.TopLeftY),
            new TileCoord(dto.BottomRightX, dto.BottomRightY));

    private static GameDateDtoV1 MapDate(GameDate date) =>
        new() { Day = date.Day, Season = date.Season.ToString(), Year = date.Year };

    private static GameDate MapDate(GameDateDtoV1 dto) =>
        new(dto.Day, Enum.Parse<Season>(dto.Season), dto.Year);

    private static string DescribeZone(Zone zone) =>
        $"{zone.LocationName}|{zone.TopLeft.X}|{zone.TopLeft.Y}|{zone.BottomRight.X}|{zone.BottomRight.Y}";
}

internal static class SortedDictionaryExtensions
{
    public static SortedDictionary<TKey, TValue> ToSortedDictionary<TKey, TValue>(
        this IDictionary<TKey, TValue> source,
        IComparer<TKey> comparer)
        where TKey : notnull =>
        new(source, comparer);
}
