using Deskling.Core.Math;
using Windows.Win32;

namespace Deskling.Platform.Windows;

/// <summary>Reads the mouse cursor in physical virtual-screen pixels (the process is PerMonitorV2 DPI aware).</summary>
public static class CursorSource
{
    public static Vec2I GetPosition() =>
        PInvoke.GetCursorPos(out var p) ? new Vec2I(p.X, p.Y) : Vec2I.Zero;
}
