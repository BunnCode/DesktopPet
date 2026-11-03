namespace DesktopPet.Core.World;

/// <summary>Derives walkable and hangable surfaces from a desktop snapshot. Pure geometry.</summary>
public static class PlatformBuilder
{
    /// <summary>Visible segments narrower than this are dropped (slivers between overlapping windows).</summary>
    public const double MinSegmentWidth = 8;

    /// <summary>Assumed title-bar height in DIPs when placing hang bars (Windows 11 captions are ~32 DIP).</summary>
    public const double DefaultCaptionHeightDip = 32;

    /// <param name="snapshot">Desktop to derive surfaces from.</param>
    /// <param name="captionHeightDip">Assumed title-bar height for hang bars.</param>
    /// <param name="includeHangBars">Whether to produce <see cref="PlatformKind.HangBar"/>s at all.</param>
    public static IReadOnlyList<Platform> Build(
        DesktopSnapshot snapshot, double captionHeightDip = DefaultCaptionHeightDip, bool includeHangBars = true)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var platforms = new List<Platform>();
        var monitors = snapshot.Monitors;

        foreach (var m in monitors)
        {
            platforms.Add(new Platform(PlatformKind.Floor, FloorKey(m), m.WorkArea.Bottom, m.WorkArea.Left, m.WorkArea.Right));
        }

        var windows = snapshot.Windows;
        for (var i = 0; i < windows.Count; i++)
        {
            var window = windows[i];
            var bounds = window.Bounds;
            if (bounds.IsEmpty)
            {
                continue;
            }

            // Stand on the top edge where there's room above it on screen...
            var topSpans = window.IsMaximized ? [] : SpansOnScreen(monitors, bounds.Left, bounds.Right, bounds.Top - 1);
            if (topSpans.Count > 0)
            {
                AddVisible(platforms, PlatformKind.WindowTop, WindowKey(window.Id), bounds.Top, topSpans, windows, i, window);
                continue;
            }

            if (!includeHangBars)
            {
                continue;
            }

            // ...otherwise (maximized / pinned to the screen top) hang from the bottom of the title bar,
            // provided the title bar itself is on screen and not covered.
            var scale = ScaleAt(monitors, bounds.Left + (bounds.Width / 2.0), bounds.Top);
            var barY = (int)System.Math.Round(bounds.Top + (captionHeightDip * scale));
            if (barY >= bounds.Bottom)
            {
                continue;
            }

            var barSpans = SpansOnScreen(monitors, bounds.Left, bounds.Right, barY - 1);
            AddVisible(platforms, PlatformKind.HangBar, HangKey(window.Id), barY, barSpans, windows, i, window);
        }

        return platforms;
    }

    public static string FloorKey(MonitorInfo monitor)
    {
        ArgumentNullException.ThrowIfNull(monitor);
        return "floor:" + monitor.Id;
    }

    public static string WindowKey(long windowId) => "window:" + windowId.ToString(System.Globalization.CultureInfo.InvariantCulture);

    public static string HangKey(long windowId) => "hang:" + windowId.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>
    /// Adds the parts of <paramref name="spans"/> at height <paramref name="y"/> not hidden by windows above
    /// <paramref name="index"/> in z-order. A window above hides the surface if it covers the row just above it
    /// (where a standing pet's body, or a title bar being gripped, is).
    /// </summary>
    private static void AddVisible(
        List<Platform> platforms, PlatformKind kind, string key, int y, List<(double Left, double Right)> spans,
        IReadOnlyList<WindowInfo> windows, int index, WindowInfo owner)
    {
        var row = kind == PlatformKind.HangBar ? y - 1 : y;
        for (var j = 0; j < index && spans.Count > 0; j++)
        {
            var above = windows[j].Bounds;
            if (above.Top <= row && row < above.Bottom)
            {
                spans = Subtract(spans, above.Left, above.Right);
            }
        }

        foreach (var (left, right) in spans)
        {
            if (right - left >= MinSegmentWidth)
            {
                platforms.Add(new Platform(kind, key, y, left, right, AnchorX: owner.Bounds.Left, AnchorWidth: owner.Bounds.Width));
            }
        }
    }

    private static double ScaleAt(IReadOnlyList<MonitorInfo> monitors, double x, double y)
    {
        foreach (var m in monitors)
        {
            if (m.Bounds.Contains(new Math.Vec2(x, y)))
            {
                return m.Scale;
            }
        }

        return monitors.Count > 0 ? monitors[0].Scale : 1.0;
    }

    /// <summary>The parts of [left, right) where pixel row <paramref name="row"/> lies inside some monitor's work area.</summary>
    private static List<(double Left, double Right)> SpansOnScreen(IReadOnlyList<MonitorInfo> monitors, double left, double right, int row)
    {
        var spans = new List<(double, double)>();
        foreach (var m in monitors)
        {
            var area = m.WorkArea;
            if (row < area.Top || row >= area.Bottom)
            {
                continue;
            }

            var l = System.Math.Max(left, area.Left);
            var r = System.Math.Min(right, area.Right);
            if (r > l)
            {
                spans.Add((l, r));
            }
        }

        return Merge(spans);
    }

    private static List<(double Left, double Right)> Subtract(List<(double Left, double Right)> spans, double cutLeft, double cutRight)
    {
        var result = new List<(double, double)>(spans.Count + 1);
        foreach (var (l, r) in spans)
        {
            if (cutRight <= l || cutLeft >= r)
            {
                result.Add((l, r));
                continue;
            }

            if (cutLeft > l)
            {
                result.Add((l, cutLeft));
            }

            if (cutRight < r)
            {
                result.Add((cutRight, r));
            }
        }

        return result;
    }

    /// <summary>Merges touching/overlapping spans (e.g. a window crossing two side-by-side monitors).</summary>
    private static List<(double Left, double Right)> Merge(List<(double Left, double Right)> spans)
    {
        if (spans.Count < 2)
        {
            return spans;
        }

        spans.Sort((a, b) => a.Left.CompareTo(b.Left));
        var merged = new List<(double Left, double Right)> { spans[0] };
        for (var i = 1; i < spans.Count; i++)
        {
            var last = merged[^1];
            if (spans[i].Left <= last.Right)
            {
                merged[^1] = (last.Left, System.Math.Max(last.Right, spans[i].Right));
            }
            else
            {
                merged.Add(spans[i]);
            }
        }

        return merged;
    }
}
