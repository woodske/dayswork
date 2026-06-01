using Dayswork.Core.Config;
using Dayswork.Core.Domain;
using Dayswork.Core.Energy;
using Dayswork.Core.Persistence.Dto;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Dayswork.Core.Persistence;

public sealed class SaveDataSerializer : ISaveDataSerializer
{
    private const int CurrentSchemaVersion = 2;
    private static readonly TileCoord CompatibilityPlaceholderTopLeft = new(0, 0);
    private static readonly TileCoord CompatibilityPlaceholderBottomRight = new(999, 999);
    private static readonly IConfigSnapshot DefaultConfigSnapshot = ConfigDefaults.Build();

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

        if (schemaVersion.Value == 1)
        {
            _logWarning("Dayswork save data schema version 1 is legacy pre-release hourly contract data and was dropped.");
            return Array.Empty<Contract>();
        }

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
            _logWarning($"Dayswork save data schema v2 payload could not be mapped — starting fresh. ({ex.Message})");
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
                _logWarning($"Skipping schema v2 contract '{contractId}': {ex.Message}");
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

    private static ContractDtoV2 MapDomainToDtoV2(Contract contract)
    {
        var authoritativeScope = contract.ScopeSelection ?? DeriveCompatibilityScopeSelection(contract);
        var authoritativeTerms = contract.TermsSnapshot ?? DeriveCompatibilityTermsSnapshot(contract);

        return new ContractDtoV2
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
            ScopeSelection = MapScopeSelection(authoritativeScope),
            TermsSnapshot = MapTermsSnapshot(authoritativeTerms),
            LegacyFinancialBridge = new LegacyFinancialBridgeDto
            {
                DepositAmount = contract.DepositAmount,
                HourlyRate = contract.HourlyRate,
            },
        };
    }

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
        var financialBridge = dto.LegacyFinancialBridge ?? throw new JsonException("LegacyFinancialBridge was null.");

        return new Contract(
            Id: id,
            EnabledTasks: enabledTasks,
            Zones: ProjectCompatibilityZones(scopeSelection),
            TaskDestinations: destinations,
            Schedule: Enum.Parse<ContractSchedule>(dto.Schedule),
            Status: Enum.Parse<ContractStatus>(dto.Status),
            HireDate: MapDate(dto.HireDate ?? throw new JsonException("HireDate was null.")),
            DepositAmount: financialBridge.DepositAmount,
            HourlyRate: financialBridge.HourlyRate,
            ScopeSelection: scopeSelection,
            TermsSnapshot: termsSnapshot);
    }

    private static ContractScopeSelection DeriveCompatibilityScopeSelection(Contract contract)
    {
        var outdoorZones = contract.Zones
            .Where(zone => string.Equals(zone.LocationName, "Farm", StringComparison.OrdinalIgnoreCase))
            .OrderBy(DescribeZone, StringComparer.Ordinal)
            .ToList();

        var greenhouseZone = contract.Zones.FirstOrDefault(zone => IsGreenhouseLocation(zone.LocationName));
        var greenhouse = greenhouseZone is null ? null : new GreenhouseSelection(greenhouseZone.LocationName);

        var animalBuildings = contract.Zones
            .Where(zone =>
                !string.Equals(zone.LocationName, "Farm", StringComparison.OrdinalIgnoreCase)
                && !IsGreenhouseLocation(zone.LocationName))
            .Select(zone => TryInferAnimalBuildingSelection(zone.LocationName))
            .Where(selection => selection is not null)
            .Cast<AnimalBuildingSelection>()
            .Distinct()
            .OrderBy(selection => selection.LocationName, StringComparer.Ordinal)
            .ThenBy(selection => selection.Tier)
            .ToList();

        return new ContractScopeSelection(
            OutdoorZones: outdoorZones.AsReadOnly(),
            AnimalBuildings: animalBuildings.AsReadOnly(),
            Greenhouse: greenhouse);
    }

    private static ContractTermsSnapshot DeriveCompatibilityTermsSnapshot(Contract contract)
    {
        var pricing = new PricingSnapshot(
            LineItems: Array.Empty<PricingLineItem>(),
            OutdoorSubtotal: 0,
            AnimalSubtotal: 0,
            GreenhouseSubtotal: 0,
            TotalPrice: contract.DepositAmount);

        var actionCosts = DefaultConfigSnapshot.WorkActionCosts
            .OrderBy(kvp => kvp.Key.ToString(), StringComparer.Ordinal)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        var energy = new WorkerEnergyProfile(DefaultConfigSnapshot.WorkerDailyEnergyCapacity, actionCosts);
        return new ContractTermsSnapshot(pricing, energy);
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
            Greenhouse = null,
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

        // Prefer the TODO-10 Greenhouses list; fall back to the legacy single Greenhouse field for
        // saves written before multi-greenhouse support.
        var greenhouses = (dto.Greenhouses is { Count: > 0 }
                ? dto.Greenhouses.Select(greenhouse => greenhouse.LocationName)
                : dto.Greenhouse is null
                    ? Enumerable.Empty<string>()
                    : new[] { dto.Greenhouse.LocationName })
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
                LineItems = snapshot.Pricing.LineItems
                    .OrderBy(line => line.Family)
                    .ThenBy(line => line.Service)
                    .ThenBy(line => line.OutdoorBand)
                    .ThenBy(line => line.AnimalTier)
                    .ThenBy(line => line.Quantity)
                    .ThenBy(line => line.UnitPrice)
                    .ThenBy(line => line.LineTotal)
                    .Select(line => new PricingLineItemDto
                    {
                        Family = line.Family.ToString(),
                        Service = line.Service.ToString(),
                        Quantity = line.Quantity,
                        UnitPrice = line.UnitPrice,
                        LineTotal = line.LineTotal,
                        OutdoorBand = line.OutdoorBand?.ToString(),
                        AnimalTier = line.AnimalTier?.ToString(),
                    })
                    .ToList(),
                OutdoorSubtotal = snapshot.Pricing.OutdoorSubtotal,
                AnimalSubtotal = snapshot.Pricing.AnimalSubtotal,
                GreenhouseSubtotal = snapshot.Pricing.GreenhouseSubtotal,
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
        var lineItems = (dto.Pricing?.LineItems ?? throw new JsonException("Pricing.LineItems was null."))
            .Select(line => new PricingLineItem(
                Family: Enum.Parse<PricingFamily>(line.Family),
                Service: Enum.Parse<TaskKind>(line.Service),
                Quantity: line.Quantity,
                UnitPrice: line.UnitPrice,
                LineTotal: line.LineTotal,
                OutdoorBand: string.IsNullOrWhiteSpace(line.OutdoorBand)
                    ? null
                    : Enum.Parse<OutdoorBandSize>(line.OutdoorBand),
                AnimalTier: string.IsNullOrWhiteSpace(line.AnimalTier)
                    ? null
                    : Enum.Parse<AnimalBuildingTier>(line.AnimalTier)))
            .OrderBy(line => line.Family)
            .ThenBy(line => line.Service)
            .ThenBy(line => line.OutdoorBand)
            .ThenBy(line => line.AnimalTier)
            .ThenBy(line => line.Quantity)
            .ThenBy(line => line.UnitPrice)
            .ThenBy(line => line.LineTotal)
            .ToList();

        var pricingSnapshotDto = dto.Pricing ?? throw new JsonException("Pricing was null.");
        var pricing = new PricingSnapshot(
            LineItems: lineItems.AsReadOnly(),
            OutdoorSubtotal: pricingSnapshotDto.OutdoorSubtotal,
            AnimalSubtotal: pricingSnapshotDto.AnimalSubtotal,
            GreenhouseSubtotal: pricingSnapshotDto.GreenhouseSubtotal,
            TotalPrice: pricingSnapshotDto.TotalPrice);

        var energyDto = dto.Energy ?? throw new JsonException("Energy was null.");
        var actionCosts = (energyDto.ActionCosts ?? throw new JsonException("Energy.ActionCosts was null."))
            .ToDictionary(
                kvp => Enum.Parse<WorkActionKind>(kvp.Key),
                kvp => kvp.Value);

        var energy = new WorkerEnergyProfile(energyDto.DailyCapacity, actionCosts);
        return new ContractTermsSnapshot(pricing, energy);
    }

    private static IReadOnlyList<Zone> ProjectCompatibilityZones(ContractScopeSelection selection)
    {
        var compatibilityZones = new List<Zone>();
        compatibilityZones.AddRange(selection.OutdoorZones);

        compatibilityZones.AddRange(selection.AnimalBuildings.Select(building =>
            new Zone(
                building.LocationName,
                CompatibilityPlaceholderTopLeft,
                CompatibilityPlaceholderBottomRight)));

        if (selection.Greenhouse is not null)
        {
            compatibilityZones.Add(new Zone(
                selection.Greenhouse.LocationName,
                CompatibilityPlaceholderTopLeft,
                CompatibilityPlaceholderBottomRight));
        }

        return compatibilityZones
            .Distinct()
            .OrderBy(DescribeZone, StringComparer.Ordinal)
            .ToList()
            .AsReadOnly();
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

    private static bool IsGreenhouseLocation(string locationName) =>
        locationName.Contains("Greenhouse", StringComparison.OrdinalIgnoreCase);

    private static AnimalBuildingSelection? TryInferAnimalBuildingSelection(string locationName)
    {
        if (string.IsNullOrWhiteSpace(locationName))
            return null;

        var normalized = locationName.Trim();
        if (normalized.Contains("Coop3", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("Deluxe Coop", StringComparison.OrdinalIgnoreCase))
        {
            return new AnimalBuildingSelection(locationName, AnimalBuildingTier.DeluxeCoop);
        }

        if (normalized.Contains("Coop2", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("Big Coop", StringComparison.OrdinalIgnoreCase))
        {
            return new AnimalBuildingSelection(locationName, AnimalBuildingTier.BigCoop);
        }

        if (normalized.Contains("Coop", StringComparison.OrdinalIgnoreCase))
            return new AnimalBuildingSelection(locationName, AnimalBuildingTier.Coop);

        if (normalized.Contains("Barn3", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("Deluxe Barn", StringComparison.OrdinalIgnoreCase))
        {
            return new AnimalBuildingSelection(locationName, AnimalBuildingTier.DeluxeBarn);
        }

        if (normalized.Contains("Barn2", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("Big Barn", StringComparison.OrdinalIgnoreCase))
        {
            return new AnimalBuildingSelection(locationName, AnimalBuildingTier.BigBarn);
        }

        if (normalized.Contains("Barn", StringComparison.OrdinalIgnoreCase))
            return new AnimalBuildingSelection(locationName, AnimalBuildingTier.Barn);

        return null;
    }

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
