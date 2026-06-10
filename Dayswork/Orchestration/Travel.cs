using Dayswork.Core.Domain;
using Dayswork.Worker;
using StardewValley;

namespace Dayswork.Orchestration;

/// <summary>One travel step: walk to a tile, then optionally warp through to another location.</summary>
internal sealed record TravelLeg(
    GameLocation WalkLocation,
    TileCoord WalkTarget,
    GameLocation? WarpTarget,
    TileCoord WarpArrivalTile);

internal enum TravelFailurePolicy
{
    /// <summary>Stop and report; the orchestrator decides (skip batch, mark trip undelivered, abort).</summary>
    ReportFailure,

    /// <summary>Never strand the worker: warp straight to the plan's final destination and complete.</summary>
    WarpToDestination,
}

internal sealed record TravelPlan(IReadOnlyList<TravelLeg> Legs, TravelFailurePolicy OnFailure);

/// <summary>
/// Drives a TravelPlan leg by leg: walk to each leg's target tile, then warp through to the leg's
/// destination. The single mover for every cross-location transition (building doors, expansion
/// hops, store entries); WorkerMovementDriver.WarpWorker stays the only teleport primitive.
/// </summary>
internal sealed class TravelRunner
{
    private readonly WorkerMovementDriver _movement;
    private TravelPlan? _plan;
    private FarmhandNpc? _worker;
    private int _legIndex;

    public TravelRunner(WorkerMovementDriver movement)
    {
        _movement = movement;
    }

    public bool IsComplete { get; private set; } = true;

    /// <summary>Only ever true for ReportFailure plans; WarpToDestination plans complete instead.</summary>
    public bool Failed { get; private set; }

    public bool CompletedWithForcedWarp { get; private set; }

    public string? FailureDetail { get; private set; }

    public void Start(TravelPlan plan, FarmhandNpc worker)
    {
        Clear();
        _plan = plan;
        _worker = worker;
        _legIndex = 0;
        IsComplete = plan.Legs.Count == 0;

        if (!IsComplete)
            StartCurrentLegWalk();
    }

    public void Update()
    {
        if (_plan is null || _worker is null || IsComplete || Failed)
            return;

        if (_movement.NavigationFailed)
        {
            ApplyFailurePolicy();
            return;
        }

        if (!_movement.HasArrived)
            return;

        var leg = _plan.Legs[_legIndex];
        if (leg.WarpTarget is not null)
            _movement.WarpWorker(_worker, leg.WalkLocation, leg.WarpTarget, leg.WarpArrivalTile);
        _legIndex++;

        if (_legIndex >= _plan.Legs.Count)
        {
            IsComplete = true;
            return;
        }

        StartCurrentLegWalk();
    }

    /// <summary>Resolve an externally detected stall (stuck escalation) through the plan's failure policy.</summary>
    public void ForceFailure()
    {
        if (_plan is null || _worker is null || IsComplete || Failed)
            return;

        ApplyFailurePolicy();
    }

    public void Clear()
    {
        _plan = null;
        _worker = null;
        _legIndex = 0;
        IsComplete = true;
        Failed = false;
        CompletedWithForcedWarp = false;
        FailureDetail = null;
    }

    private void StartCurrentLegWalk()
    {
        var leg = _plan!.Legs[_legIndex];
        _movement.StartNavigation(leg.WalkTarget, leg.WalkLocation, _worker!);
    }

    private void ApplyFailurePolicy()
    {
        var plan = _plan!;
        var leg = plan.Legs[Math.Clamp(_legIndex, 0, plan.Legs.Count - 1)];
        FailureDetail =
            $"leg {_legIndex + 1}/{plan.Legs.Count}: walk to ({leg.WalkTarget.X},{leg.WalkTarget.Y}) " +
            $"in {leg.WalkLocation.NameOrUniqueName} failed";
        DevLog.Log($"[Dayswork][travel] {FailureDetail}; policy={plan.OnFailure}.");

        if (plan.OnFailure == TravelFailurePolicy.WarpToDestination)
        {
            var final = plan.Legs[^1];
            var destination = final.WarpTarget ?? final.WalkLocation;
            var arrivalTile = final.WarpTarget is not null ? final.WarpArrivalTile : final.WalkTarget;
            _movement.WarpWorker(_worker!, _worker!.currentLocation ?? leg.WalkLocation, destination, arrivalTile);
            CompletedWithForcedWarp = true;
            IsComplete = true;
            return;
        }

        Failed = true;
    }
}
