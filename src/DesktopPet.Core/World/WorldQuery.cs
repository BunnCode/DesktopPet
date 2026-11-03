using DesktopPet.Core.Math;

namespace DesktopPet.Core.World;

/// <summary>Spatial questions about the monitor layout. The virtual screen is a union of rectangles, not one rectangle.</summary>
public static class WorldQuery
{
    /// <summary>The monitor containing the pet's body. Feet sit on an exclusive bottom edge, so test 1px above them.</summary>
    public static MonitorInfo? MonitorAt(IReadOnlyList<MonitorInfo> monitors, Vec2 feet)
    {
        ArgumentNullException.ThrowIfNull(monitors);
        var body = BodyPoint(feet);
        return monitors.FirstOrDefault(m => m.Bounds.Contains(body));
    }

    public static MonitorInfo? Nearest(IReadOnlyList<MonitorInfo> monitors, Vec2 feet)
    {
        ArgumentNullException.ThrowIfNull(monitors);
        var body = BodyPoint(feet);
        return monitors.MinBy(m => DistanceSquared(m.Bounds, body));
    }

    /// <summary>
    /// Whether a walking pet's leading edge may occupy <paramref name="x"/> at feet height <paramref name="feetY"/>:
    /// true if that spot is inside some monitor's work area. Everything else is a wall: screen edges, side taskbars,
    /// and a neighbour monitor whose floor is higher than ours. A neighbour with a lower floor is open (the pet drops down).
    /// </summary>
    public static bool IsWalkable(IReadOnlyList<MonitorInfo> monitors, double x, double feetY)
    {
        ArgumentNullException.ThrowIfNull(monitors);
        var probe = new Vec2(x, feetY - 1);
        foreach (var m in monitors)
        {
            if (m.WorkArea.Contains(probe))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// True when the pet is somewhere it can never recover from by falling: beside every monitor, below every
    /// monitor in its column, or in a vertical gap. Being above the top of a monitor is fine; gravity brings it back.
    /// </summary>
    public static bool IsLost(IReadOnlyList<MonitorInfo> monitors, Vec2 feet)
    {
        ArgumentNullException.ThrowIfNull(monitors);
        var body = BodyPoint(feet);
        if (monitors.Any(m => m.Bounds.Contains(body)))
        {
            return false;
        }

        var column = monitors.Where(m => body.X >= m.Bounds.Left && body.X < m.Bounds.Right).ToList();
        if (column.Count == 0)
        {
            return true;
        }

        return body.Y >= column.Min(m => m.Bounds.Top);
    }

    private static Vec2 BodyPoint(Vec2 feet) => new(feet.X, feet.Y - 1);

    private static double DistanceSquared(RectI r, Vec2 p)
    {
        var dx = System.Math.Max(System.Math.Max(r.Left - p.X, 0), p.X - (r.Right - 1));
        var dy = System.Math.Max(System.Math.Max(r.Top - p.Y, 0), p.Y - (r.Bottom - 1));
        return (dx * dx) + (dy * dy);
    }
}
