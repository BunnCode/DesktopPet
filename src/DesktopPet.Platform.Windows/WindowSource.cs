using DesktopPet.Core.World;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;
using Windows.Win32.UI.WindowsAndMessaging;

namespace DesktopPet.Platform.Windows;

/// <summary>
/// Enumerates top-level windows that should count as platforms, in z-order (topmost first),
/// with their visible frame bounds in physical pixels.
/// </summary>
public static class WindowSource
{
    /// <summary>Shell windows that are always "there" but aren't things to stand on.</summary>
    private static readonly HashSet<string> IgnoredClasses = new(StringComparer.Ordinal)
    {
        "Progman",                     // desktop
        "WorkerW",                     // desktop wallpaper host
        "Shell_TrayWnd",               // taskbar (its top is already the work-area floor)
        "Shell_SecondaryTrayWnd",      // taskbar on other monitors
        "Windows.UI.Core.CoreWindow",  // shell flyouts (Start, search, ...)
    };

    /// <param name="excludeProcessId">Our own process: the pet and its overlays must never be platforms.</param>
    public static IReadOnlyList<WindowInfo> GetWindows(uint excludeProcessId)
    {
        var windows = new List<WindowInfo>();

        BOOL Visit(HWND hwnd, LPARAM data)
        {
            if (TryDescribe(hwnd, excludeProcessId) is { } window)
            {
                windows.Add(window);
            }

            return true; // continue
        }

        PInvoke.EnumWindows(Visit, default); // enumerates in z-order, top to bottom
        return windows;
    }

    private static unsafe WindowInfo? TryDescribe(HWND hwnd, uint excludeProcessId)
    {
        if (!PInvoke.IsWindowVisible(hwnd) || PInvoke.IsIconic(hwnd))
        {
            return null;
        }

        uint processId;
        _ = PInvoke.GetWindowThreadProcessId(hwnd, &processId);
        if (processId == excludeProcessId)
        {
            return null;
        }

        var exStyle = (WINDOW_EX_STYLE)PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        var isAppWindow = (exStyle & WINDOW_EX_STYLE.WS_EX_APPWINDOW) != 0;

        // Tool windows, click-through overlays and never-activating popups (toasts, on-screen keyboards) aren't "real" windows.
        if (!isAppWindow && (exStyle & (WINDOW_EX_STYLE.WS_EX_TOOLWINDOW | WINDOW_EX_STYLE.WS_EX_NOACTIVATE)) != 0)
        {
            return null;
        }

        if ((exStyle & WINDOW_EX_STYLE.WS_EX_TRANSPARENT) != 0)
        {
            return null;
        }

        // Untitled visible windows are almost always invisible helpers.
        if (PInvoke.GetWindowTextLength(hwnd) == 0)
        {
            return null;
        }

        // Cloaked: on another virtual desktop, or a suspended UWP app. Visible by the API's standards, not on screen.
        int cloaked = 0;
        if (PInvoke.DwmGetWindowAttribute(hwnd, DWMWINDOWATTRIBUTE.DWMWA_CLOAKED, &cloaked, sizeof(int)).Succeeded && cloaked != 0)
        {
            return null;
        }

        const int classBufferLength = 64;
        var className = stackalloc char[classBufferLength];
        var classLength = PInvoke.GetClassName(hwnd, className, classBufferLength);
        if (classLength > 0 && IgnoredClasses.Contains(new string(className, 0, classLength)))
        {
            return null;
        }

        // The visible frame. GetWindowRect includes invisible resize borders (~7 px each side on Windows 10/11),
        // which would make the pet hover above the title bar.
        RECT rect;
        if (PInvoke.DwmGetWindowAttribute(hwnd, DWMWINDOWATTRIBUTE.DWMWA_EXTENDED_FRAME_BOUNDS, &rect, (uint)sizeof(RECT)).Failed &&
            !PInvoke.GetWindowRect(hwnd, out rect))
        {
            return null;
        }

        var bounds = MonitorSource.ToRect(rect);
        if (bounds.IsEmpty)
        {
            return null;
        }

        return new WindowInfo((long)(nint)hwnd.Value, bounds, IsMaximized: PInvoke.IsZoomed(hwnd));
    }
}
