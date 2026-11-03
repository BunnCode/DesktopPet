using DesktopPet.Core.Math;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace DesktopPet.Platform.Windows;

/// <summary>Win32 operations on the pet's own window. All coordinates are physical pixels.</summary>
public static class PetWindowInterop
{
    /// <summary>
    /// Hide from Alt-Tab and the taskbar (tool window) and never take focus when clicked (no-activate).
    /// The layered style that gives per-pixel alpha and click-through is set by WPF (AllowsTransparency).
    /// </summary>
    public static void ApplyPetStyles(nint hwnd)
    {
        var h = new HWND(hwnd);
        var exStyle = (WINDOW_EX_STYLE)PInvoke.GetWindowLong(h, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        exStyle |= WINDOW_EX_STYLE.WS_EX_TOOLWINDOW | WINDOW_EX_STYLE.WS_EX_NOACTIVATE;
        exStyle &= ~WINDOW_EX_STYLE.WS_EX_APPWINDOW;
        _ = PInvoke.SetWindowLong(h, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, (int)exStyle);
    }

    /// <summary>Moves/resizes without touching z-order or activation. Bypasses WPF's DIP-based Left/Top.</summary>
    public static void SetBounds(nint hwnd, RectI bounds) =>
        PInvoke.SetWindowPos(
            new HWND(hwnd), HWND.Null,
            bounds.Left, bounds.Top, bounds.Width, bounds.Height,
            SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE | SET_WINDOW_POS_FLAGS.SWP_NOZORDER | SET_WINDOW_POS_FLAGS.SWP_NOOWNERZORDER);

    /// <summary>Puts the window back at the top of the topmost band (other topmost windows and the taskbar can cover it).</summary>
    public static void BringToTopmost(nint hwnd) =>
        PInvoke.SetWindowPos(
            new HWND(hwnd), HWND.HWND_TOPMOST,
            0, 0, 0, 0,
            SET_WINDOW_POS_FLAGS.SWP_NOMOVE | SET_WINDOW_POS_FLAGS.SWP_NOSIZE | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE);
}
