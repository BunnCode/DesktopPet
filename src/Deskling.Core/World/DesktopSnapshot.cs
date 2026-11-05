namespace Deskling.Core.World;

/// <summary>
/// Everything Core knows about the desktop at one moment. Produced by the platform layer (or a test fake);
/// Core never queries the OS itself.
/// </summary>
/// <param name="Monitors">Displays.</param>
/// <param name="Windows">Visible top-level windows in z-order, <b>topmost first</b>.</param>
public sealed record DesktopSnapshot(IReadOnlyList<MonitorInfo> Monitors, IReadOnlyList<WindowInfo> Windows)
{
    public DesktopSnapshot(IReadOnlyList<MonitorInfo> monitors)
        : this(monitors, [])
    {
    }

    public static DesktopSnapshot Empty { get; } = new([], []);
}
