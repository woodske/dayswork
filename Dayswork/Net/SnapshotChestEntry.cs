namespace Dayswork.Net;

/// <summary>An off-farm chest as the host found it, on its way into a
/// <see cref="Dayswork.Core.Net.MenuSnapshotResponseMessage"/>.</summary>
internal sealed record SnapshotChestEntry(
    string LocationName,
    string LocationDisplayName,
    int TileX,
    int TileY,
    string DisplayName,
    bool IsAutoGrabber);
