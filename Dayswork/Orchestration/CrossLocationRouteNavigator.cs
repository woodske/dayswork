using Dayswork.Compat;
using Dayswork.Core.Compat;
using Dayswork.Worker;
using StardewValley;

namespace Dayswork.Orchestration;

internal sealed class CrossLocationRouteNavigator
{
    private readonly WorkerMovementDriver _movement;
    private ValidatedExpansionRoute? _route;
    private FarmhandNpc? _worker;
    private int _hopIndex;

    public CrossLocationRouteNavigator(WorkerMovementDriver movement)
    {
        _movement = movement;
    }

    public bool IsActive => _route is not null && !IsComplete && !NavigationFailed;

    public bool IsComplete { get; private set; } = true;

    public bool NavigationFailed { get; private set; }

    public ExpansionRouteFailure? Failure { get; private set; }

    public void Start(ValidatedExpansionRoute route, FarmhandNpc worker)
    {
        Clear();
        _route = route;
        _worker = worker;
        _hopIndex = 0;
        IsComplete = route.Hops.Count == 0;
        NavigationFailed = false;
        Failure = null;

        if (!IsComplete)
            StartCurrentHopNavigation();
    }

    public void Update()
    {
        if (_route is null || _worker is null || IsComplete || NavigationFailed)
            return;

        if (_movement.NavigationFailed)
        {
            FailCurrentHop();
            return;
        }

        if (!_movement.HasArrived)
            return;

        var current = _route.Hops[_hopIndex];
        _movement.WarpWorker(_worker, current.Source, current.Target, current.Hop.ArrivalTile);
        _hopIndex++;

        if (_hopIndex >= _route.Hops.Count)
        {
            IsComplete = true;
            return;
        }

        StartCurrentHopNavigation();
    }

    public void Clear()
    {
        _route = null;
        _worker = null;
        _hopIndex = 0;
        IsComplete = true;
        NavigationFailed = false;
        Failure = null;
    }

    private void StartCurrentHopNavigation()
    {
        var current = _route!.Hops[_hopIndex];
        _movement.StartNavigation(current.Hop.ApproachTile, current.Source, _worker!);
    }

    private void FailCurrentHop()
    {
        var definition = _route!.Definition;
        var hop = _route.Hops[Math.Clamp(_hopIndex, 0, _route.Hops.Count - 1)].Hop;
        Failure = new ExpansionRouteFailure(
            new ExpansionRouteRequest(
                definition.FarmSignature,
                definition.SourceLocationName,
                definition.TargetLocationName,
                definition.Purpose),
            definition.Id,
            hop.Ordinal,
            ExpansionRouteFailureReason.NavigationFailed,
            $"navigation_failed_{hop.SourceLocationName}_{hop.ApproachTile.X}_{hop.ApproachTile.Y}");
        NavigationFailed = true;
    }
}
