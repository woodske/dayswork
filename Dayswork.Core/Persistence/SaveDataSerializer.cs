using Dayswork.Core.Domain;
using Dayswork.Core.Persistence.Dto;
using Newtonsoft.Json;

namespace Dayswork.Core.Persistence;

public sealed class SaveDataSerializer : ISaveDataSerializer
{
    private static readonly JsonSerializerSettings _serializerSettings = new()
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
        var dtos = contracts.Select(MapDomainToDto).ToList();
        var envelope = new DaysworkSaveDataV1
        {
            SchemaVersion = 1,
            ModVersion = modVersion,
            Contracts = dtos,
        };
        return JsonConvert.SerializeObject(envelope, _serializerSettings);
    }

    public IReadOnlyList<Contract> Deserialize(string? json)
    {
        if (string.IsNullOrEmpty(json))
            return Array.Empty<Contract>();

        DaysworkSaveDataV1? envelope;
        try
        {
            envelope = JsonConvert.DeserializeObject<DaysworkSaveDataV1>(json, _serializerSettings);
        }
        catch (JsonException ex)
        {
            _logWarning($"Dayswork save data could not be parsed — starting fresh. ({ex.Message})");
            return Array.Empty<Contract>();
        }

        if (envelope is null)
        {
            _logWarning("Dayswork save data deserialized to null — starting fresh.");
            return Array.Empty<Contract>();
        }

        if (envelope.SchemaVersion > 1)
        {
            _logWarning($"Dayswork save data schema version {envelope.SchemaVersion} is newer than this mod supports (v1). Contracts not loaded — please update the mod.");
            return Array.Empty<Contract>();
        }

        var results = new List<Contract>();
        foreach (var dto in envelope.Contracts)
        {
            try
            {
                results.Add(MapDtoToDomain(dto));
            }
            catch (Exception ex)
            {
                _logWarning($"Skipping contract '{dto.Id}': {ex.Message}");
            }
        }

        return results.AsReadOnly();
    }

    private static ContractDtoV1 MapDomainToDto(Contract contract)
    {
        return new ContractDtoV1
        {
            Id = contract.Id.Value.ToString(),
            EnabledTasks = contract.EnabledTasks.Select(t => t.ToString()).ToList(),
            Zones = contract.Zones.Select(MapZone).ToList(),
            TaskDestinations = contract.TaskDestinations.ToDictionary(
                kvp => kvp.Key.ToString(),
                kvp => MapDestinationToDto(kvp.Value)),
            Schedule = contract.Schedule.ToString(),
            Status = contract.Status.ToString(),
            HireDate = MapDate(contract.HireDate),
            DepositAmount = contract.DepositAmount,
            HourlyRate = contract.HourlyRate,
        };
    }

    private static Contract MapDtoToDomain(ContractDtoV1 dto)
    {
        var id = new ContractId(Guid.Parse(dto.Id));

        var enabledTasks = dto.EnabledTasks
            .Select(s => Enum.Parse<TaskKind>(s))
            .ToHashSet();

        var zones = dto.Zones.Select(MapZone).ToList();

        var destinations = dto.TaskDestinations.ToDictionary(
            kvp => Enum.Parse<TaskKind>(kvp.Key),
            kvp => MapDestinationToDomain(kvp.Value));

        var schedule = Enum.Parse<ContractSchedule>(dto.Schedule);
        var status = Enum.Parse<ContractStatus>(dto.Status);
        var hireDate = MapDate(dto.HireDate);

        return new Contract(
            Id: id,
            EnabledTasks: enabledTasks,
            Zones: zones.AsReadOnly(),
            TaskDestinations: destinations,
            Schedule: schedule,
            Status: status,
            HireDate: hireDate,
            DepositAmount: dto.DepositAmount,
            HourlyRate: dto.HourlyRate);
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
        new(dto.LocationName, new TileCoord(dto.TopLeftX, dto.TopLeftY), new TileCoord(dto.BottomRightX, dto.BottomRightY));

    private static GameDateDtoV1 MapDate(GameDate date) =>
        new() { Day = date.Day, Season = date.Season.ToString(), Year = date.Year };

    private static GameDate MapDate(GameDateDtoV1 dto) =>
        new(dto.Day, Enum.Parse<Season>(dto.Season), dto.Year);
}
