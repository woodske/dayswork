using Dayswork.Core.Domain;
using Dayswork.Core.Shifts;
using StardewValley;

namespace Dayswork.Orchestration;

internal sealed class IndoorWorkScanner
{
    private readonly WorkAreaScanner _workAreaScanner;

    public IndoorWorkScanner(WorkAreaScanner workAreaScanner) =>
        _workAreaScanner = workAreaScanner;

    public IReadOnlyList<WorkItem> ScanInterior(
        GameLocation interior,
        IReadOnlySet<TaskKind> enabled,
        ToolSnapshot snapshot)
    {
        var layer = interior.Map.Layers[0];
        var wholeInterior = new Zone(
            interior.Name,
            new TileCoord(0, 0),
            new TileCoord(Math.Max(0, layer.LayerWidth - 1), Math.Max(0, layer.LayerHeight - 1)));

        return _workAreaScanner.ScanZones(
            interior,
            new[] { wholeInterior },
            enabled,
            snapshot,
            new TileCoord(0, 0));
    }
}
