using Dayswork.Core.Domain;
using Dayswork.Core.Energy;
using Dayswork.Core.Pathing;
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

    // Gate tiles on the current route (openable gates the worker will pass through), and the subset
    // the worker has actually opened and not yet closed. Gates are opened lazily as the worker
    // reaches them (never all at once at plan time) and every opened gate is closed — on pass, or on
    // Clear/Warp if the route is abandoned mid-walk — so none leaks open (a leaked off-screen gate
    // never auto-closes; see docs/fences-and-gates.md).
    private readonly HashSet<Point> _routeGateTiles = new();
    private readonly HashSet<Point> _openedGates = new();

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

        RecordRouteGates(controller.pathToEndPoint, location);

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
        if (GridPathfinder.TryFindRoute(new LivePassabilityView(location), source, destination, out var route))
        {
            routeCost = route.Count;
            return true;
        }

        routeCost = 0;
        return false;
    }

    // Live (uncached) whole-map route-cost flood fill. The hot selection call sites route through
    // the per-shift LocationPassabilityCache instead (which reuses one PassabilityGrid per location);
    // this static entry point stays for the navigation fallback and one-off queries that must reflect
    // the current world.
    public static IReadOnlyDictionary<TileCoord, int> ComputeRouteCostsFrom(TileCoord source, GameLocation location) =>
        GridPathfinder.ComputeRouteCosts(new LivePassabilityView(location), source);

    public void Update()
    {
        if (_worker is null || HasArrived || NavigationFailed)
            return;

        while (_waypoints.Count > 0)
        {
            var target = _waypoints.Peek();
            OpenGateIfApproaching(target); // the next waypoint is ≤1 tile away — open it as we arrive
            var delta = target - _worker.Position;
            var dist = delta.Length();

            if (dist > 0.01f)
            {
                StepToward(target, delta, dist);
                return;
            }

            _worker.Position = target;
            _waypoints.Dequeue();
            TryCloseGate(target);
        }

        HasArrived = true;
        _worker.StopTaskAnimation();
        CloseTrackedGates(); // route done — close anything opened but somehow not passed
    }

    public void Clear()
    {
        // Route abandoned (new nav, stuck recovery, travel cancel, warp) — close any gate the worker
        // opened but hasn't walked through, or it leaks open indefinitely off-screen.
        CloseTrackedGates();
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
            TryCloseGate(target);
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
            // A closed fence gate blocks isCollidingPosition, but the worker opens it on approach
            // (see OpenGateIfApproaching), so route through it as if passable.
            return HasOpenableGate(tile, location);

        return true;
    }

    // Record (don't open) the openable gate tiles on a freshly-planned route. They're opened lazily
    // on approach in Update, matching how a player reads the animation, instead of all popping open
    // the moment the path is planned.
    private void RecordRouteGates(IEnumerable<Point> tiles, GameLocation location)
    {
        foreach (var tile in tiles)
            if (HasOpenableGate(tile, location))
                _routeGateTiles.Add(tile);
    }

    // Open the next waypoint's gate as the worker arrives at it (≤1 tile away), tracking it so it's
    // guaranteed to be closed later. Only gates the worker actually opens are tracked — a gate the
    // player already left open is not "owned" and isn't force-closed on route abandon.
    private void OpenGateIfApproaching(Vector2 pixelPos)
    {
        var location = _worker?.currentLocation;
        if (location is null) return;

        var tile = new Point((int)(pixelPos.X / TileSize), (int)(pixelPos.Y / TileSize));
        if (!_routeGateTiles.Contains(tile) || _openedGates.Contains(tile)) return;

        if (location.objects.TryGetValue(new Vector2(tile.X, tile.Y), out var obj)
            && obj is Fence fence && fence.isGate.Value
            && fence.health.Value > 1f && fence.gatePosition.Value < 88)
        {
            fence.toggleGate(open: true);
            _openedGates.Add(tile);
        }
    }

    private void TryCloseGate(Vector2 pixelPos)
    {
        var location = _worker?.currentLocation;
        if (location is null) return;
        var point = new Point((int)(pixelPos.X / TileSize), (int)(pixelPos.Y / TileSize));
        var tile = new Vector2(point.X, point.Y);
        if (location.objects.TryGetValue(tile, out var obj)
            && obj is Fence fence && fence.isGate.Value
            && fence.health.Value > 1f && fence.gatePosition.Value >= 88)
            fence.toggleGate(open: false);
        _openedGates.Remove(point); // handled (closed here, or already closed) — no longer owed a close
    }

    // Close every gate the worker opened and hasn't yet closed, except one it's currently standing on
    // (closing a gate onto the worker would visually clip it). Clears the route-gate tracking.
    private void CloseTrackedGates()
    {
        var location = _worker?.currentLocation;
        if (location is not null && _openedGates.Count > 0)
        {
            var workerTile = _worker!.TilePoint;
            foreach (var tile in _openedGates)
            {
                if (tile == workerTile) continue;
                if (location.objects.TryGetValue(new Vector2(tile.X, tile.Y), out var obj)
                    && obj is Fence fence && fence.isGate.Value
                    && fence.health.Value > 1f && fence.gatePosition.Value >= 88)
                    fence.toggleGate(open: false);
            }
        }

        _openedGates.Clear();
        _routeGateTiles.Clear();
    }

    private static bool HasOpenableGate(Point tile, GameLocation location) =>
        location.objects.TryGetValue(new Vector2(tile.X, tile.Y), out var obj)
        && obj is Fence fence && fence.isGate.Value && fence.health.Value > 1f;

    private bool TryEnqueueFallbackRoute(TileCoord destination, GameLocation location, FarmhandNpc worker)
    {
        var start = new TileCoord(worker.TilePoint.X, worker.TilePoint.Y);

        // Live probe — the fallback route is about to be physically walked, so it must reflect the
        // current world, never a possibly-stale cached grid.
        if (!GridPathfinder.TryFindRoute(new LivePassabilityView(location), start, destination, out var route))
            return false;

        RecordRouteGates(route.Select(t => new Point(t.X, t.Y)), location);

        foreach (var tile in route)
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
}
