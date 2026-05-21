using Dayswork.Core.Domain;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Pathfinding;

namespace Dayswork.Worker;

internal sealed class WorkerMovementDriver
{
    private const float TileSize = 64f;
    private const float BaseWalkPixelsPerTick = 4f;
    private const float WalkFrameIntervalMs = 120f;

    private readonly Queue<Vector2> _waypoints = new();
    private FarmhandNpc? _worker;

    public bool HasArrived { get; private set; } = true;
    public bool NavigationFailed { get; private set; }
    public bool UsedDirectFallback { get; private set; }

    public void StartNavigation(TileCoord destination, GameLocation location, FarmhandNpc worker)
    {
        Clear();

        _worker = worker;
        worker.currentLocation = location;
        worker.StopTaskAnimation();
        HasArrived = false;
        UsedDirectFallback = false;

        var controller = new PathFindController(
            worker,
            location,
            new Point(destination.X, destination.Y),
            -1);

        if (controller.pathToEndPoint is null)
        {
            if (TryEnqueueFallbackRoute(destination, location, worker))
            {
                UsedDirectFallback = true;
                MarkArrivedIfNoWaypoints(worker);
                return;
            }

            NavigationFailed = true;
            return;
        }

        if (controller.pathToEndPoint.Any(point => !IsTilePassableForWorker(point, location)))
        {
            if (TryEnqueueFallbackRoute(destination, location, worker))
            {
                UsedDirectFallback = true;
                MarkArrivedIfNoWaypoints(worker);
                return;
            }

            NavigationFailed = true;
            return;
        }

        foreach (var point in controller.pathToEndPoint)
            _waypoints.Enqueue(ToPixel(point));

        if (_waypoints.Count == 0)
        {
            worker.Position = ToPixel(destination);
            worker.StopTaskAnimation();
            HasArrived = true;
        }
    }

    public void StartForcedPixelRoute(GameLocation location, FarmhandNpc worker, params Vector2[] pixelWaypoints)
    {
        Clear();

        _worker = worker;
        worker.currentLocation = location;
        worker.StopTaskAnimation();
        HasArrived = false;

        foreach (var point in pixelWaypoints)
            _waypoints.Enqueue(point);

        MarkArrivedIfNoWaypoints(worker);
    }

    public void Update()
    {
        if (_worker is null || HasArrived || NavigationFailed)
            return;

        while (_waypoints.Count > 0)
        {
            var target = _waypoints.Peek();
            var delta = target - _worker.Position;
            var dist = delta.Length();

            if (dist > 0.01f)
            {
                StepToward(target, delta, dist);
                return;
            }

            _worker.Position = target;
            _waypoints.Dequeue();
        }

        HasArrived = true;
        _worker.StopTaskAnimation();
    }

    public void Clear()
    {
        _waypoints.Clear();
        _worker = null;
        HasArrived = true;
        NavigationFailed = false;
        UsedDirectFallback = false;
    }

    private void StepToward(Vector2 target, Vector2 delta, float distance)
    {
        var step = Math.Min(BaseWalkPixelsPerTick, distance);
        _worker!.Position += Vector2.Normalize(delta) * step;
        var direction = FacingFrom(delta);
        _worker.faceDirection(direction);
        _worker.Sprite.Animate(Game1.currentGameTime, WalkFrameStartFor(direction), 4, WalkFrameIntervalMs);

        if (step >= distance)
        {
            _worker.Position = target;
            _waypoints.Dequeue();
        }
    }

    private void MarkArrivedIfNoWaypoints(FarmhandNpc worker)
    {
        if (_waypoints.Count > 0)
            return;

        worker.StopTaskAnimation();
        HasArrived = true;
    }

    private static Vector2 ToPixel(TileCoord tile) =>
        new(tile.X * TileSize, tile.Y * TileSize);

    private static Vector2 ToPixel(Point tile) =>
        new(tile.X * TileSize, tile.Y * TileSize);

    public static bool IsTilePassableForWorker(Point tile, GameLocation location)
    {
        if (!location.isTilePassable(new xTile.Dimensions.Location(tile.X, tile.Y), Game1.viewport))
            return false;

        if (location is Farm farm && farm.buildings.Any(building => building.occupiesTile(tile.X, tile.Y, false)))
            return false;

        return true;
    }

    private bool TryEnqueueFallbackRoute(TileCoord destination, GameLocation location, FarmhandNpc worker)
    {
        var start = worker.TilePoint;
        var end   = new Point(destination.X, destination.Y);

        if (start == end)
            return true;

        if (!IsWithinMap(end, location) || !IsTilePassableForWorker(end, location))
            return false;

        var queue = new Queue<Point>();
        var cameFrom = new Dictionary<Point, Point?>();
        queue.Enqueue(start);
        cameFrom[start] = null;

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current == end)
                break;

            foreach (var next in Neighbours(current))
            {
                if (cameFrom.ContainsKey(next) ||
                    !IsWithinMap(next, location) ||
                    !IsTilePassableForWorker(next, location))
                    continue;

                cameFrom[next] = current;
                queue.Enqueue(next);
            }
        }

        if (!cameFrom.ContainsKey(end))
            return false;

        foreach (var tile in ReconstructPath(end, cameFrom))
            _waypoints.Enqueue(ToPixel(tile));

        return true;
    }

    private static int FacingFrom(Vector2 delta)
    {
        if (Math.Abs(delta.X) > Math.Abs(delta.Y))
            return delta.X > 0 ? 1 : 3;

        return delta.Y > 0 ? 2 : 0;
    }

    private static int WalkFrameStartFor(int facingDirection) =>
        facingDirection switch
        {
            0 => 8,
            1 => 4,
            2 => 0,
            3 => 12,
            _ => 0,
        };

    private static IEnumerable<Point> Neighbours(Point tile)
    {
        yield return new Point(tile.X, tile.Y - 1);
        yield return new Point(tile.X + 1, tile.Y);
        yield return new Point(tile.X, tile.Y + 1);
        yield return new Point(tile.X - 1, tile.Y);
    }

    private static bool IsWithinMap(Point tile, GameLocation location)
    {
        var layer = location.Map.Layers[0];
        return tile.X >= 0 &&
               tile.Y >= 0 &&
               tile.X < layer.LayerWidth &&
               tile.Y < layer.LayerHeight;
    }

    private static IEnumerable<Point> ReconstructPath(Point end, Dictionary<Point, Point?> cameFrom)
    {
        var route = new Stack<Point>();
        var current = end;
        while (cameFrom[current] is { } previous)
        {
            route.Push(current);
            current = previous;
        }

        return route;
    }
}
