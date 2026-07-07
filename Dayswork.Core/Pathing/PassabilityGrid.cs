using Dayswork.Core.Domain;

namespace Dayswork.Core.Pathing;

/// <summary>
/// A precomputed passability snapshot for one location, row-major packed. Built once from a
/// per-tile probe (the game side feeds <c>WorkerMovementDriver.IsTilePassableForWorker</c>) and
/// then reused for every route-cost query against that location within a shift, so the expensive
/// <c>isCollidingPosition</c> sweep is paid once instead of per query. Individual cells can be
/// re-probed via <see cref="SetPassable"/> when the world changes (the per-shift cache's
/// invalidation path).
/// </summary>
public sealed class PassabilityGrid : IPassabilityView
{
    private readonly bool[] _passable; // row-major: index = y * Width + x

    public int Width { get; }

    public int Height { get; }

    /// <summary>Builds the grid by probing every tile once. Used by the game side.</summary>
    public PassabilityGrid(int width, int height, Func<int, int, bool> probe)
    {
        Width = width;
        Height = height;
        _passable = new bool[width * height];
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                _passable[y * width + x] = probe(x, y);
    }

    /// <summary>Builds the grid from a literal <c>[x, y]</c> map. Used by unit tests.</summary>
    public PassabilityGrid(bool[,] passable)
    {
        Width = passable.GetLength(0);
        Height = passable.GetLength(1);
        _passable = new bool[Width * Height];
        for (var x = 0; x < Width; x++)
            for (var y = 0; y < Height; y++)
                _passable[y * Width + x] = passable[x, y];
    }

    public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

    /// <inheritdoc/>
    public bool IsPassable(int x, int y) => _passable[y * Width + x];

    public bool IsPassable(TileCoord tile) => InBounds(tile.X, tile.Y) && _passable[tile.Y * Width + tile.X];

    /// <summary>
    /// Re-set a single tile's passability after the world changed under it. Out-of-bounds writes
    /// are ignored. Used by the per-shift cache's invalidation path, which re-probes the game and
    /// writes the fresh value here instead of discarding the whole grid.
    /// </summary>
    public void SetPassable(int x, int y, bool value)
    {
        if (InBounds(x, y)) _passable[y * Width + x] = value;
    }
}
