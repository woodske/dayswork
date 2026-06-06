using Microsoft.Xna.Framework;
using StardewValley.Menus;

namespace Dayswork.UI.Layout;

internal sealed class LayoutContext
{
    private readonly List<Registration> _registrations = new();
    private int _nextId = 1000;

    public IScrollTarget? ScrollTarget { get; set; }

    public ClickableComponent Register(Rectangle bounds, string name, string label, Action? onClick)
    {
        var comp = new ClickableComponent(bounds, name, label) { myID = _nextId++ };
        _registrations.Add(new Registration(comp, onClick));
        return comp;
    }

    public void WireNavigation()
    {
        if (_registrations.Count == 0)
            return;

        var sorted = _registrations
            .OrderBy(r => r.Component.bounds.Center.Y)
            .ThenBy(r => r.Component.bounds.Center.X)
            .ToList();

        var rows = GroupIntoRows(sorted);

        foreach (var row in rows)
        {
            for (var i = 0; i < row.Count; i++)
            {
                if (i > 0)
                    row[i].Component.leftNeighborID = row[i - 1].Component.myID;
                if (i < row.Count - 1)
                    row[i].Component.rightNeighborID = row[i + 1].Component.myID;
            }
        }

        for (var r = 0; r < rows.Count; r++)
        {
            foreach (var reg in rows[r])
            {
                if (r > 0)
                    reg.Component.upNeighborID = FindNearestByX(rows[r - 1], reg.Component.bounds.Center.X);
                if (r < rows.Count - 1)
                    reg.Component.downNeighborID = FindNearestByX(rows[r + 1], reg.Component.bounds.Center.X);
            }
        }
    }

    public List<ClickableComponent> GetAllComponents() =>
        _registrations.Select(r => r.Component).ToList();

    public bool HandleClick(int x, int y)
    {
        foreach (var reg in _registrations)
        {
            if (!reg.Component.bounds.Contains(x, y) || reg.OnClick is null)
                continue;

            reg.OnClick();
            return true;
        }

        return false;
    }

    public ClickableComponent? GetDefaultSnapTarget() =>
        _registrations.Count > 0 ? _registrations[0].Component : null;

    private static List<List<Registration>> GroupIntoRows(List<Registration> sorted)
    {
        var rows = new List<List<Registration>>();
        if (sorted.Count == 0)
            return rows;

        var minHeight = sorted.Min(r => Math.Max(1, r.Component.bounds.Height));
        var tolerance = minHeight / 2;

        var currentRow = new List<Registration> { sorted[0] };
        var currentY = sorted[0].Component.bounds.Center.Y;

        for (var i = 1; i < sorted.Count; i++)
        {
            var y = sorted[i].Component.bounds.Center.Y;
            if (Math.Abs(y - currentY) <= tolerance)
            {
                currentRow.Add(sorted[i]);
            }
            else
            {
                rows.Add(currentRow);
                currentRow = new List<Registration> { sorted[i] };
                currentY = y;
            }
        }

        rows.Add(currentRow);
        return rows;
    }

    private static int FindNearestByX(List<Registration> row, int targetX)
    {
        var best = row[0];
        var bestDist = Math.Abs(row[0].Component.bounds.Center.X - targetX);

        for (var i = 1; i < row.Count; i++)
        {
            var dist = Math.Abs(row[i].Component.bounds.Center.X - targetX);
            if (dist < bestDist)
            {
                best = row[i];
                bestDist = dist;
            }
        }

        return best.Component.myID;
    }

    private sealed record Registration(ClickableComponent Component, Action? OnClick);
}
