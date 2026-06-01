using Dayswork.Core.Domain;

namespace Dayswork.Core.Compat;

public readonly record struct ExpansionRouteId(string Value)
{
    public override string ToString() => Value;
}

public enum ExpansionRoutePurpose
{
    WorkEntry,
    DepositEntry,
    ReturnToFarm,
}

public enum ExpansionLocationRole
{
    GreenhouseWork,
    DepositOnly,
}

public enum ExpansionRouteFailureReason
{
    UnsupportedFarmSignature,
    RouteNotDefined,
    SourceLocationMissing,
    TargetLocationMissing,
    ApproachTileInvalid,
    ArrivalTileInvalid,
    ApproachTileBlocked,
    ArrivalTileBlocked,
    NavigationFailed,
}

public sealed record ExpansionLocationDescriptor(
    string LocationName,
    string DisplayName,
    ExpansionLocationRole Role,
    string AssociatedWorkLocationName)
{
    public bool IsWorkScopeEligible => Role == ExpansionLocationRole.GreenhouseWork;

    public bool IsDepositDestinationEligible =>
        Role is ExpansionLocationRole.GreenhouseWork or ExpansionLocationRole.DepositOnly;
}

public sealed record ExpansionRouteRequest(
    FarmMapSignature FarmSignature,
    string SourceLocationName,
    string TargetLocationName,
    ExpansionRoutePurpose Purpose);

public sealed record ExpansionRouteDefinition(
    ExpansionRouteId Id,
    FarmMapSignature FarmSignature,
    string SourceLocationName,
    string TargetLocationName,
    ExpansionRoutePurpose Purpose,
    IReadOnlyList<ExpansionRouteHop> Hops);

public sealed record ExpansionRouteHop(
    int Ordinal,
    string SourceLocationName,
    TileCoord ApproachTile,
    string TargetLocationName,
    TileCoord ArrivalTile);

public sealed record ExpansionRouteFailure(
    ExpansionRouteRequest Request,
    ExpansionRouteId? RouteId,
    int? HopOrdinal,
    ExpansionRouteFailureReason Reason,
    string Detail);
