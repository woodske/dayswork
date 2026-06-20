using Dayswork.Core.Domain;
using Dayswork.Core.Energy;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Pathfinding;

namespace Dayswork.Worker;

internal sealed class WorkerMovementDriver
{
    private const float TileSize = 64f;
    private const float WalkFrameIntervalMs = 120f;

    private readonly Queue<Vector2> _waypoints = new();
    private FarmhandNpc? _worker;
    private float _walkPixelsPerTick = 2f;

    public bool HasArrived { get; private set; } = true;
    public bool NavigationFailed { get; private set; }
    public bool UsedDirectFallback { get; private set; }

    public void SetPacingProfile(WorkerPacingProfile profile)
    {
        _walkPixelsPerTick = profile.WalkPixelsPerTick;
    }

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

        OpenGatesAlongRoute(controller.pathToEndPoint, location);

        foreach (var point in controller.pathToEndPoint)
            _waypoints.Enqueue(ToPixel(point));

        if (_waypoints.Count == 0)
        {
            worker.Position = ToPixel(destination);
            worker.StopTaskAnimation();
            HasArrived = true;
        }
    }

    public void WarpWorker(FarmhandNpc worker, GameLocation from, GameLocation to, TileCoord entryTile)
    {
        Clear();

        from.characters.Remove(worker);
        to.characters.Remove(worker);
        to.addCharacter(worker);
        worker.currentLocation = to;
        worker.Position = ToPixel(entryTile);
        worker.StopTaskAnimation();
        HasArrived = true;
    }

    public bool TryGetRouteCost(
        TileCoord destination,
        GameLocation location,
        FarmhandNpc worker,
        out int routeCost) =>
        TryGetRouteCost(
            new TileCoord(worker.TilePoint.X, worker.TilePoint.Y),
            destination,
            location,
            out routeCost);

    public static bool TryGetRouteCost(
        TileCoord source,
        TileCoord destination,
        GameLocation location,
        out int routeCost)
    {
        var start = new Point(source.X, source.Y);
        var end = new Point(destination.X, destination.Y);
        if (TryFindRoute(start, end, location, out var route))
        {
            routeCost = route.Count;
            return true;
        }

        routeCost = 0;
        return false;
    }

    public static IReadOnlyDictionary<TileCoord, int> ComputeRouteCostsFrom(TileCoord source, GameLocation location)
    {
        var start = new Point(source.X, source.Y);
        var queue = new Queue<Point>();
        var visited = new HashSet<Point> { start };
        var routeCosts = new Dictionary<TileCoord, int>
        {
            [source] = 0,
        };

        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            var currentCoord = new TileCoord(current.X, current.Y);
            var currentCost = routeCosts[currentCoord];

            foreach (var next in Neighbours(current))
            {
                if (!visited.Add(next) ||
                    !IsWithinMap(next, location) ||
                    !IsTilePassableForWorker(next, location))
                    continue;

                routeCosts[new TileCoord(next.X, next.Y)] = currentCost + 1;
                queue.Enqueue(next);
            }
        }

        return routeCosts;
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
        var step = Math.Min(_walkPixelsPerTick, distance);
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
        // Tile-map check: permanent walls, water, map boundaries.
        if (!location.isTilePassable(new xTile.Dimensions.Location(tile.X, tile.Y), Game1.viewport))
            return false;

        // Physical collision check: buildings, fences, machines, resource clumps, furniture, etc.
        // Use inset rect (+1/62) to match PathFindController.findPath corner math — a full 64x64 rect
        // maps right/bottom edges to the ADJACENT tile (X+1, Y+1), causing false positives on tiles
        // next to buildings or objects (e.g. FarmEntrance tile falsely blocked by shipping bin above it).
        // pathfinding: true matches PathFindController.findPath, which passes pathfinding: true. For a
        // null character this flag gates only the player-farmer collision block, so passing true makes
        // the worker ignore the player when routing (the player is never a pathing obstacle).
        // ignoreCharacterRequirement skips the guard that would otherwise return true for null character.
        var bounds = new Rectangle(tile.X * 64 + 1, tile.Y * 64 + 1, 62, 62);
        if (location.isCollidingPosition(bounds, Game1.viewport,
                isFarmer: false, damagesFarmer: 0, glider: false,
                character: null, pathfinding: true,
                ignoreCharacterRequirement: true))
            // A closed fence gate blocks isCollidingPosition, but the worker can open it
            // (see OpenGatesAlongRoute), so route through it as if passable.
            return HasOpenableGate(tile, location);

        return true;
    }

    private static bool HasOpenableGate(Point tile, GameLocation location) =>
        location.objects.TryGetValue(new Vector2(tile.X, tile.Y), out var obj)
        && obj is Fence fence && fence.isGate.Value && fence.health.Value > 1f;

    private static void OpenGatesAlongRoute(IEnumerable<Point> tiles, GameLocation location)
    {
        foreach (var tile in tiles)
        {
            if (location.objects.TryGetValue(new Vector2(tile.X, tile.Y), out var obj)
                && obj is Fence fence && fence.isGate.Value
                && fence.health.Value > 1f && fence.gatePosition.Value < 88)
                fence.toggleGate(open: true);
        }
    }

    private bool TryEnqueueFallbackRoute(TileCoord destination, GameLocation location, FarmhandNpc worker)
    {
        var start = worker.TilePoint;
        var end   = new Point(destination.X, destination.Y);

        if (!TryFindRoute(start, end, location, out var route))
            return false;

        OpenGatesAlongRoute(route, location);

        foreach (var tile in route)
            _waypoints.Enqueue(ToPixel(tile));

        return true;
    }

    private static bool TryFindRoute(
        Point start,
        Point end,
        GameLocation location,
        out IReadOnlyList<Point> route)
    {
        if (start == end)
        {
            route = Array.Empty<Point>();
            return true;
        }

        if (!IsWithinMap(end, location) || !IsTilePassableForWorker(end, location))
        {
            route = Array.Empty<Point>();
            return false;
        }

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
        {
            route = Array.Empty<Point>();
            return false;
        }

        route = ReconstructPath(end, cameFrom).ToList();
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
