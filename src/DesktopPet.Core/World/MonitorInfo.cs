using DesktopPet.Core.Math;

namespace DesktopPet.Core.World;

/// <summary>One display, in physical virtual-screen pixels.</summary>
/// <param name="Id">Stable identity across snapshots (Win32 device name, e.g. <c>\\.\DISPLAY1</c>).</param>
/// <param name="Bounds">Full monitor rectangle.</param>
/// <param name="WorkArea">Monitor minus taskbar and app bars.</param>
/// <param name="Scale">Effective DPI / 96 (1.0, 1.25, 1.5, ...).</param>
/// <param name="IsPrimary">Whether this is the primary display.</param>
public sealed record MonitorInfo(string Id, RectI Bounds, RectI WorkArea, double Scale, bool IsPrimary);
