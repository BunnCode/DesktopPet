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

    public FakeDesktop Monitor(int x, int y, int width, int height, double scale = 1.0, int taskbar = 0, bool? primary = null, string? id = null)
    {
        var bounds = RectI.FromSize(x, y, width, height);
        var work = bounds with { Bottom = bounds.Bottom - taskbar };
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

    public DesktopSnapshot Snapshot() => new(_monitors.ToArray());
}
