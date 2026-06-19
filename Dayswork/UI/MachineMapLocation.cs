using Dayswork.Core.Domain;

namespace Dayswork.UI;

/// <summary>
/// One location the machine-selection map session can display: its name, a friendly label for the
/// location switcher, and the machine tiles present there (tile → qualified id). UI-only.
/// </summary>
internal sealed record MachineMapLocation(
    string LocationName,
    string DisplayName,
    IReadOnlyDictionary<TileCoord, string> Candidates);
