using Dayswork.Core.Domain;

namespace Dayswork.UI;

/// <summary>
/// One location the chest-selection map session can display: its name, a friendly label for the
/// location switcher, and the chest tiles present there (tile → ChestRef). UI-only.
/// </summary>
internal sealed record ChestMapLocation(
    string LocationName,
    string DisplayName,
    IReadOnlyDictionary<TileCoord, ChestRef> Candidates)
{
    /// <summary>
    /// Groups a flat chest list (from <see cref="Integration.ChestResolver"/>) into one entry per
    /// game location, preserving each chest's <see cref="ChestRef"/>. Only locations that contain a
    /// chest appear, so the switcher never lands on an empty map.
    /// </summary>
    internal static List<ChestMapLocation> Group(IEnumerable<ChestEntry> entries) =>
        entries
            .GroupBy(entry => entry.Ref.LocationName, StringComparer.Ordinal)
            .Select(group => new ChestMapLocation(
                group.Key,
                group.First().GroupLabel,
                group.ToDictionary(entry => entry.Ref.Tile, entry => entry.Ref)))
            .ToList();
}

/// <summary>
/// Which non-chest options a chest picker offers as on-screen buttons in the map view. Each picker
/// enables only the ones that make sense for it (e.g. machine input has just "None").
/// </summary>
internal readonly record struct ChestPickerOptions(
    bool ShowAutomatic,
    bool ShowShippingBin,
    bool ShowNone);
