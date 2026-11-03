using System.Globalization;

namespace DesktopPet.Core.World;

/// <summary>Derives climbable walls from a desktop snapshot. Pure geometry, mirroring <see cref="PlatformBuilder"/> for verticals.</summary>
public static class WallBuilder
{
    /// <summary>Visible wall segments shorter than this are dropped.</summary>
    public const double MinSegmentHeight = 16;

    public static IReadOnlyList<Wall> Build(DesktopSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var walls = new List<Wall>();
        var monitors = snapshot.Monitors;
        var windows = snapshot.Windows;

        // Screen edges: a work-area edge is a wall where the column just outside it isn't in any work area.
        foreach (var m in monitors)
        {
            var area = m.WorkArea;
            AddEdge(walls, monitors, m, area.Left, outsideColumn: area.Left - 1, WallSide.PetOnRight, "L");
            AddEdge(walls, monitors, m, area.Right, outsideColumn: area.Right, WallSide.PetOnLeft, "R");
        }

        // Window sides, outside faces only. Visible where the edge pixel column isn't covered by a window in front
        // and the column the pet would cling in is on screen.
        for (var i = 0; i < windows.Count; i++)
        {
            var window = windows[i];
            var b = window.Bounds;
            if (window.IsMaximized || b.IsEmpty)
            {
                continue;
            }

            AddSide(walls, monitors, windows, i, x: b.Left, edgeColumn: b.Left, outsideColumn: b.Left - 1, WallSide.PetOnLeft, "L");
            AddSide(walls, monitors, windows, i, x: b.Right, edgeColumn: b.Right - 1, outsideColumn: b.Right, WallSide.PetOnRight, "R");
        }

        return walls;
    }

    public static string SideKey(long windowId, string side) => $"wall:{windowId.ToString(CultureInfo.InvariantCulture)}:{side}";

    private static void AddEdge(List<Wall> walls, IReadOnlyList<MonitorInfo> monitors, MonitorInfo m, int x, int outsideColumn, WallSide side, string tag)
    {
        var spans = new List<(double Top, double Bottom)> { (m.WorkArea.Top, m.WorkArea.Bottom) };
        foreach (var other in monitors)
        {
            var area = other.WorkArea;
            if (outsideColumn >= area.Left && outsideColumn < area.Right)
            {
                spans = Subtract(spans, area.Top, area.Bottom); // the screen continues there: not a wall
            }
        }

        var key = $"edge:{m.Id}:{tag}";
        foreach (var (top, bottom) in spans)
        {
            if (bottom - top >= MinSegmentHeight)
            {
                walls.Add(new Wall(WallKind.ScreenEdge, key, x, top, bottom, side, WindowId: null, Owner: default));
            }
        }
    }

    private static void AddSide(
        List<Wall> walls, IReadOnlyList<MonitorInfo> monitors, IReadOnlyList<WindowInfo> windows, int index,
        int x, int edgeColumn, int outsideColumn, WallSide side, string tag)
    {
        var window = windows[index];
        var b = window.Bounds;

        // Clip to work areas containing the column the pet clings in.
        var spans = new List<(double Top, double Bottom)>();
        foreach (var m in monitors)
        {
            var area = m.WorkArea;
            if (outsideColumn < area.Left || outsideColumn >= area.Right)
            {
                continue;
            }

            var top = System.Math.Max(b.Top, area.Top);
            var bottom = System.Math.Min(b.Bottom, area.Bottom);
            if (bottom > top)
            {
                spans.Add((top, bottom));
            }
        }

        // Windows in front covering the edge column hide it.
        for (var j = 0; j < index && spans.Count > 0; j++)
        {
            var above = windows[j].Bounds;
            if (edgeColumn >= above.Left && edgeColumn < above.Right)
            {
                spans = Subtract(spans, above.Top, above.Bottom);
            }
        }

        var key = SideKey(window.Id, tag);
        foreach (var (top, bottom) in spans)
        {
            if (bottom - top >= MinSegmentHeight)
            {
                walls.Add(new Wall(WallKind.WindowSide, key, x, top, bottom, side, window.Id, b));
            }
        }
    }

    private static List<(double Top, double Bottom)> Subtract(List<(double Top, double Bottom)> spans, double cutTop, double cutBottom)
    {
        var result = new List<(double, double)>(spans.Count + 1);
        foreach (var (t, b) in spans)
        {
            if (cutBottom <= t || cutTop >= b)
            {
                result.Add((t, b));
                continue;
            }

            if (cutTop > t)
            {
                result.Add((t, cutTop));
            }

            if (cutBottom < b)
            {
                result.Add((cutBottom, b));
            }
        }

        return result;
    }
}
