namespace Dayswork.Core.Crops;

using Dayswork.Core.Domain;

public sealed record FieldState
{
    public string LocationName { get; }
    public GameDate Date { get; }
    public bool IsSeasonAgnosticLocation { get; }
    public IReadOnlyList<TileState> Tiles { get; }

    public FieldState(
        string locationName,
        GameDate date,
        bool isSeasonAgnosticLocation,
        IReadOnlyList<TileState>? tiles)
    {
        LocationName = locationName;
        Date = date;
        IsSeasonAgnosticLocation = isSeasonAgnosticLocation;
        Tiles = (tiles ?? Array.Empty<TileState>())
            .OrderBy(tile => tile.Tile.Y)
            .ThenBy(tile => tile.Tile.X)
            .ToList()
            .AsReadOnly();
    }
}
