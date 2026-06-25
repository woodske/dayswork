namespace Dayswork.Core.FishPonds;

using Dayswork.Core.Domain;
using Dayswork.Core.Persistence.Dto;
using Newtonsoft.Json;

public static class FishPondWorkScopeSerialization
{
    public static FishPondWorkScopeDtoV1? MapDomainToDto(FishPondWorkScope scope)
    {
        if (!scope.IsEnabled)
            return null;

        return new FishPondWorkScopeDtoV1
        {
            Ponds = scope.Ponds
                .OrderBy(pond => pond.LocationName, StringComparer.Ordinal)
                .ThenBy(pond => pond.Tile.Y)
                .ThenBy(pond => pond.Tile.X)
                .Select(MapPondToDto)
                .ToList(),
            OutputDestination = MapDestinationToDto(scope.OutputDestination),
        };
    }

    public static FishPondWorkScope MapDtoToDomain(FishPondWorkScopeDtoV1? dto)
    {
        if (dto is null || dto.Ponds.Count == 0)
            return FishPondWorkScope.Empty;

        return new FishPondWorkScope(
            dto.Ponds.Select(MapPondToDomain).ToList(),
            MapDestinationToDomain(dto.OutputDestination));
    }

    private static FishPondRefDtoV1 MapPondToDto(FishPondRef pond) =>
        new()
        {
            LocationName = pond.LocationName,
            X = pond.Tile.X,
            Y = pond.Tile.Y,
        };

    private static FishPondRef MapPondToDomain(FishPondRefDtoV1 dto) =>
        new(Require(dto.LocationName, "FishPond LocationName"), new TileCoord(dto.X, dto.Y));

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
                Require(dto.LocationName, "FishPond OutputDestination LocationName"),
                new TileCoord(
                    dto.X ?? throw new JsonException("FishPond OutputDestination missing X."),
                    dto.Y ?? throw new JsonException("FishPond OutputDestination missing Y.")))),
            "ShippingBin" => ShippingBinDestination.Instance,
            "AutomaticOutput" => AutomaticOutputDestination.Instance,
            _ => throw new JsonException($"Unknown destination type: '{dto.Type}'."),
        };
    }

    private static string Require(string? value, string name) =>
        string.IsNullOrWhiteSpace(value) ? throw new JsonException($"{name} was null or empty.") : value;
}
