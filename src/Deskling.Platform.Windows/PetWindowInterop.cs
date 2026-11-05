using Deskling.Core.Math;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Deskling.Platform.Windows;

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

    /// <summary>
    /// For the debug overlay: like <see cref="ApplyPetStyles"/>, plus WS_EX_TRANSPARENT so every click goes straight through.
    /// </summary>
    public static void ApplyOverlayStyles(nint hwnd)
    {
        var h = new HWND(hwnd);
        var exStyle = (WINDOW_EX_STYLE)PInvoke.GetWindowLong(h, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        exStyle |= WINDOW_EX_STYLE.WS_EX_TOOLWINDOW | WINDOW_EX_STYLE.WS_EX_NOACTIVATE
            | WINDOW_EX_STYLE.WS_EX_TRANSPARENT | WINDOW_EX_STYLE.WS_EX_LAYERED;
        exStyle &= ~WINDOW_EX_STYLE.WS_EX_APPWINDOW;
        _ = PInvoke.SetWindowLong(h, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, (int)exStyle);
    }

    /// <summary>Moves/resizes without touching z-order or activation. Bypasses WPF's DIP-based Left/Top.</summary>
    public static void SetBounds(nint hwnd, RectI bounds) =>
        PInvoke.SetWindowPos(
            new HWND(hwnd), HWND.Null,
            bounds.Left, bounds.Top, bounds.Width, bounds.Height,
            SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE | SET_WINDOW_POS_FLAGS.SWP_NOZORDER | SET_WINDOW_POS_FLAGS.SWP_NOOWNERZORDER);

    /// <summary>Moves without resizing (a window that sizes itself to its content), physical pixels.</summary>
    public static void SetPosition(nint hwnd, int left, int top) =>
        PInvoke.SetWindowPos(
            new HWND(hwnd), HWND.Null,
            left, top, 0, 0,
            SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE | SET_WINDOW_POS_FLAGS.SWP_NOZORDER | SET_WINDOW_POS_FLAGS.SWP_NOOWNERZORDER |
            SET_WINDOW_POS_FLAGS.SWP_NOSIZE);

    /// <summary>
    /// Puts the window directly below <paramref name="above"/> in z-order (a pet shut in a pen, drawn behind the pen).
    /// Below a topmost window it stays topmost. Does nothing when it's already there.
    /// </summary>
    public static void PlaceBelow(nint hwnd, nint above)
    {
        if (PInvoke.GetWindow(new HWND(above), GET_WINDOW_CMD.GW_HWNDNEXT) == new HWND(hwnd))
        {
            return;
        }

        PInvoke.SetWindowPos(
            new HWND(hwnd), new HWND(above),
            0, 0, 0, 0,
            SET_WINDOW_POS_FLAGS.SWP_NOMOVE | SET_WINDOW_POS_FLAGS.SWP_NOSIZE | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE |
            SET_WINDOW_POS_FLAGS.SWP_NOOWNERZORDER);
    }

    /// <summary>Puts the window back at the top of the topmost band (other topmost windows and the taskbar can cover it).</summary>
    public static void BringToTopmost(nint hwnd) =>
        PInvoke.SetWindowPos(
            new HWND(hwnd), HWND.HWND_TOPMOST,
            0, 0, 0, 0,
            SET_WINDOW_POS_FLAGS.SWP_NOMOVE | SET_WINDOW_POS_FLAGS.SWP_NOSIZE | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE);
}
