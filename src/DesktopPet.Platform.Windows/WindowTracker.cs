using DesktopPet.Core.Math;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;
using Windows.Win32.UI.WindowsAndMessaging;

namespace DesktopPet.Platform.Windows;

/// <summary>Where another window is, for something anchored to it (a sticky note).</summary>
public enum TrackedWindowState
{
    /// <summary>Closed.</summary>
    Gone,

    /// <summary>Minimized, hidden, or on another virtual desktop.</summary>
    Hidden,

    /// <summary>On screen at the returned bounds.</summary>
    Shown,
}

/// <summary>Follows one window by id: its state and visible frame, and keeps one of ours just above it in z-order.</summary>
public static class WindowTracker
{
    /// <summary>The window's state and, when shown, its visible frame in physical pixels.</summary>
    public static unsafe (TrackedWindowState State, RectI Bounds) Query(long windowId)
    {
        var hwnd = new HWND((nint)windowId);
        if (!PInvoke.IsWindow(hwnd))
        {
            return (TrackedWindowState.Gone, default);
        }

        int cloaked = 0;
        if (!PInvoke.IsWindowVisible(hwnd) || PInvoke.IsIconic(hwnd) ||
            (PInvoke.DwmGetWindowAttribute(hwnd, DWMWINDOWATTRIBUTE.DWMWA_CLOAKED, &cloaked, sizeof(int)).Succeeded && cloaked != 0))
        {
            return (TrackedWindowState.Hidden, default);
        }

        RECT rect;
        if (PInvoke.DwmGetWindowAttribute(hwnd, DWMWINDOWATTRIBUTE.DWMWA_EXTENDED_FRAME_BOUNDS, &rect, (uint)sizeof(RECT)).Failed &&
            !PInvoke.GetWindowRect(hwnd, out rect))
        {
            return (TrackedWindowState.Hidden, default);
        }

        return (TrackedWindowState.Shown, MonitorSource.ToRect(rect));
    }

    /// <summary>
    /// Puts our window directly above <paramref name="windowId"/> (another window, or another of ours) in z-order,
    /// without activating anything, so it's covered by whatever covers that window. Does nothing when it's already there.
    /// </summary>
    public static void PlaceAbove(nint ours, long windowId)
    {
        var target = new HWND((nint)windowId);
        var prev = PInvoke.GetWindow(target, GET_WINDOW_CMD.GW_HWNDPREV);
        if (prev == new HWND(ours))
        {
            return;
        }

        // Inserting after a topmost window would make ours topmost too: at the top of the normal band, use HWND_TOP.
        var targetTopmost = IsTopmost(target);
        HWND after = prev.IsNull
            ? (targetTopmost ? HWND.HWND_TOPMOST : HWND.HWND_TOP)
            : (!targetTopmost && IsTopmost(prev) ? HWND.HWND_TOP : prev);

        PInvoke.SetWindowPos(
            new HWND(ours), after, 0, 0, 0, 0,
            SET_WINDOW_POS_FLAGS.SWP_NOMOVE | SET_WINDOW_POS_FLAGS.SWP_NOSIZE | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE |
            SET_WINDOW_POS_FLAGS.SWP_NOOWNERZORDER);
    }

    private static bool IsTopmost(HWND hwnd) =>
        ((WINDOW_EX_STYLE)PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE) & WINDOW_EX_STYLE.WS_EX_TOPMOST) != 0;
}
