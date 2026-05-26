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
        ToolSnapshot snapshot,
        OutputScopeProvenance? provenance = null)
    {
        return _workAreaScanner.ScanWholeLocation(
            interior,
            enabled,
            snapshot,
            new TileCoord(0, 0),
            provenance);
    }
}
