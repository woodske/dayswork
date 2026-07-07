using Dayswork.Core.Pathing;
using Microsoft.Xna.Framework;
using StardewValley;

namespace Dayswork.Worker;

/// <summary>
/// An <see cref="IPassabilityView"/> that probes the live game every call via
/// <see cref="WorkerMovementDriver.IsTilePassableForWorker"/>. Used where the answer must reflect
/// the current world and must not trust the per-shift <c>LocationPassabilityCache</c> — namely the
/// navigation fallback BFS (which is about to physically walk the route) and any one-off route
/// query. The pathfinder bounds-checks before probing, so only reachable tiles are touched and this
/// stays as cheap as the pre-cache behaviour.
/// </summary>
internal sealed class LivePassabilityView : IPassabilityView
{
    private readonly GameLocation _location;

    public LivePassabilityView(GameLocation location)
    {
        _location = location;
        var layer = location.Map.Layers[0];
        Width = layer.LayerWidth;
        Height = layer.LayerHeight;
    }

    public int Width { get; }

    public int Height { get; }

    public bool IsPassable(int x, int y) =>
        WorkerMovementDriver.IsTilePassableForWorker(new Point(x, y), _location);
}
