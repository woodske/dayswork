namespace Dayswork.Core.Domain;

public sealed record OutdoorWorkScope(IReadOnlyList<Zone> NormalizedZones, int TotalTileCount);
