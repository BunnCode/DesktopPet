using Deskling.Core.World;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Deskling.Platform.Windows;

/// <summary>Carries out the simulation's requests to move other windows (the pet pushing them).</summary>
public static class WindowManipulator
{
    /// <summary>
    /// Moves a window by up to (dx, dy) physical pixels without activating it or changing its z-order.
    /// The move is first clamped by <see cref="WindowMoveGuard"/> against the window's <i>current</i> visible frame and
    /// its monitor's work area, so a window can never be pushed off screen, whatever was requested.
    /// Asynchronous, so a hung app can't stall the pet. Returns false if nothing moved: the window is gone, minimized,
    /// maximized, already at the edge, or belongs to an elevated app (UIPI refuses silently; the simulation notices).
    /// </summary>
    public static unsafe bool MoveBy(long windowId, int dx, int dy)
    {
        var hwnd = new HWND((nint)windowId);
        if (!PInvoke.IsWindow(hwnd) || PInvoke.IsIconic(hwnd) || PInvoke.IsZoomed(hwnd) || !PInvoke.GetWindowRect(hwnd, out var outer))
        {
            return false;
        }

        // Visible frame (without invisible resize borders) and the work area of the monitor it's on, right now.
        RECT frameRect;
        if (PInvoke.DwmGetWindowAttribute(hwnd, DWMWINDOWATTRIBUTE.DWMWA_EXTENDED_FRAME_BOUNDS, &frameRect, (uint)sizeof(RECT)).Failed)
        {
            frameRect = outer;
        }

        var monitor = PInvoke.MonitorFromWindow(hwnd, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
        var info = new MONITORINFO { cbSize = (uint)sizeof(MONITORINFO) };
        if (!PInvoke.GetMonitorInfo(monitor, &info))
        {
            return false;
        }

        var (safeDx, safeDy) = WindowMoveGuard.Clamp(MonitorSource.ToRect(frameRect), MonitorSource.ToRect(info.rcWork), dx, dy);
        if (safeDx == 0 && safeDy == 0)
        {
            return false;
        }

        return PInvoke.SetWindowPos(
            hwnd, HWND.Null, outer.left + safeDx, outer.top + safeDy, 0, 0,
            SET_WINDOW_POS_FLAGS.SWP_NOSIZE | SET_WINDOW_POS_FLAGS.SWP_NOZORDER | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE
            | SET_WINDOW_POS_FLAGS.SWP_NOOWNERZORDER | SET_WINDOW_POS_FLAGS.SWP_ASYNCWINDOWPOS);
    }
}
