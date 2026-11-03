using DesktopPet.Core.Math;
using DesktopPet.Core.World;

namespace DesktopPet.Core.Tests.Fakes;

/// <summary>
/// Fluent builder for desktop layouts in physical pixels.
/// <c>new FakeDesktop().Monitor(0, 0, 1920, 1080, taskbar: 48).Snapshot()</c>.
/// The first monitor added is primary unless another is marked.
/// </summary>
internal sealed class FakeDesktop
{
    private readonly List<MonitorInfo> _monitors = [];
    private readonly List<WindowInfo> _windows = [];

    /// <summary>Adds a window <b>behind</b> all windows added so far (first added = topmost).</summary>
    public FakeDesktop Window(long id, int x, int y, int width, int height, bool maximized = false)
    {
        _windows.Add(new WindowInfo(id, RectI.FromSize(x, y, width, height), maximized));
        return this;
    }

    /// <param name="x">Left edge.</param>
    /// <param name="y">Top edge.</param>
    /// <param name="width">Width.</param>
    /// <param name="height">Height.</param>
    /// <param name="scale">DPI scale.</param>
    /// <param name="taskbar">Height of a bottom taskbar (shrinks the work area).</param>
    /// <param name="primary">Mark as primary; defaults to the first monitor added.</param>
    /// <param name="id">Device name; defaults to <c>\\.\DISPLAYn</c>.</param>
    /// <param name="workArea">Explicit work area (e.g. side taskbar); overrides <paramref name="taskbar"/>.</param>
    public FakeDesktop Monitor(
        int x, int y, int width, int height, double scale = 1.0, int taskbar = 0, bool? primary = null, string? id = null, RectI? workArea = null)
    {
        var bounds = RectI.FromSize(x, y, width, height);
        var work = workArea ?? bounds with { Bottom = bounds.Bottom - taskbar };
        var isPrimary = primary ?? _monitors.Count == 0;
        if (isPrimary)
        {
            for (var i = 0; i < _monitors.Count; i++)
            {
                _monitors[i] = _monitors[i] with { IsPrimary = false };
            }
        }

        _monitors.Add(new MonitorInfo(id ?? $@"\\.\DISPLAY{_monitors.Count + 1}", bounds, work, scale, isPrimary));
        return this;
    }

    public DesktopSnapshot Snapshot() => new(_monitors.ToArray(), _windows.ToArray());
}
