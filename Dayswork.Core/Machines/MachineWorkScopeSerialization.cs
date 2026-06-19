namespace Dayswork.Core.Machines;

using Dayswork.Core.Domain;
using Dayswork.Core.Persistence.Dto;
using Newtonsoft.Json;

public static class MachineWorkScopeSerialization
{
    public static MachineWorkScopeDtoV1? MapDomainToDto(MachineWorkScope scope)
    {
        if (!scope.IsEnabled)
            return null;

        return new MachineWorkScopeDtoV1
        {
            Groups = scope.Groups
                .OrderBy(group => group.Id, StringComparer.Ordinal)
                .Select(MapGroupToDto)
                .ToList(),
        };
    }

    public static MachineWorkScope MapDtoToDomain(MachineWorkScopeDtoV1? dto)
    {
        if (dto is null || dto.Groups.Count == 0)
            return MachineWorkScope.Empty;

        return new MachineWorkScope(dto.Groups.Select(MapGroupToDomain).ToList());
    }

    private static MachineGroupDtoV1 MapGroupToDto(MachineGroup group) =>
        new()
        {
            Id = group.Id,
            MachineType = group.MachineType ?? "",
            Machines = group.Machines
                .OrderBy(machine => machine.LocationName, StringComparer.Ordinal)
                .ThenBy(machine => machine.Tile.Y)
                .ThenBy(machine => machine.Tile.X)
                .Select(MapMachineToDto)
                .ToList(),
            InputFilter = new MachineInputFilterDtoV1
            {
                IsAny = group.InputFilter.IsAny,
                AllowedQualifiedIds = group.InputFilter.AllowedQualifiedIds.ToList(),
            },
            InputChest = group.InputChest is null ? null : MapChestRefToDto(group.InputChest),
            OutputDestination = MapDestinationToDto(group.OutputDestination),
            Mode = group.Mode.ToString(),
        };

    private static MachineGroup MapGroupToDomain(MachineGroupDtoV1 dto)
    {
        var id = string.IsNullOrWhiteSpace(dto.Id)
            ? throw new JsonException("Machine group Id was null or empty.")
            : dto.Id;

        var machines = (dto.Machines ?? throw new JsonException("Machine group Machines was null."))
            .Select(MapMachineToDomain)
            .ToList();

        var filterDto = dto.InputFilter ?? new MachineInputFilterDtoV1();
        var filter = filterDto.IsAny
            ? MachineInputFilter.Any
            : MachineInputFilter.Specific(filterDto.AllowedQualifiedIds ?? new List<string>());

        var mode = string.IsNullOrWhiteSpace(dto.Mode)
            ? MachineGroupMode.CollectAndReload
            : Enum.Parse<MachineGroupMode>(dto.Mode);

        return new MachineGroup(
            id,
            machines,
            filter,
            dto.InputChest is null ? null : MapChestRefToDomain(dto.InputChest),
            MapDestinationToDomain(dto.OutputDestination),
            mode,
            string.IsNullOrWhiteSpace(dto.MachineType) ? null : dto.MachineType);
    }

    private static MachineRefDtoV1 MapMachineToDto(MachineRef machine) =>
        new()
        {
            LocationName = machine.LocationName,
            X = machine.Tile.X,
            Y = machine.Tile.Y,
            ExpectedQualifiedId = machine.ExpectedQualifiedId,
        };

    private static MachineRef MapMachineToDomain(MachineRefDtoV1 dto) =>
        new(
            Require(dto.LocationName, "Machine LocationName"),
            new TileCoord(dto.X, dto.Y),
            Require(dto.ExpectedQualifiedId, "Machine ExpectedQualifiedId"));

    private static ChestRefDtoV1 MapChestRefToDto(ChestRef chestRef) =>
        new()
        {
            LocationName = chestRef.LocationName,
            X = chestRef.Tile.X,
            Y = chestRef.Tile.Y,
        };

    private static ChestRef MapChestRefToDomain(ChestRefDtoV1 dto) =>
        new(Require(dto.LocationName, "Machine InputChest LocationName"), new TileCoord(dto.X, dto.Y));

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
            AutomaticOutputDestination => new DestinationDtoV1 { Type = "AutomaticOutput" },
            _ => throw new JsonException($"Unknown DestinationKey type: {destination.GetType().Name}"),
        };

    private static DestinationKey MapDestinationToDomain(DestinationDtoV1? dto)
    {
        if (dto is null || string.IsNullOrWhiteSpace(dto.Type))
            return AutomaticOutputDestination.Instance;

        return dto.Type switch
        {
            "Chest" => new ChestDestination(new ChestRef(
                Require(dto.LocationName, "Machine OutputDestination LocationName"),
                new TileCoord(
                    dto.X ?? throw new JsonException("Machine OutputDestination missing X."),
                    dto.Y ?? throw new JsonException("Machine OutputDestination missing Y.")))),
            "ShippingBin" => ShippingBinDestination.Instance,
            "AutomaticOutput" => AutomaticOutputDestination.Instance,
            _ => throw new JsonException($"Unknown destination type: '{dto.Type}'."),
        };
    }

    private static string Require(string? value, string name) =>
        string.IsNullOrWhiteSpace(value) ? throw new JsonException($"{name} was null or empty.") : value;
}
