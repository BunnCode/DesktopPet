using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using DesktopPet.Platform.Windows;

namespace DesktopPet.App.UI;

/// <summary>
/// Notification-area icon. Essential: the pet has no taskbar button or Alt-Tab entry, so this is how you reach it
/// and quit. Uses WinForms' NotifyIcon; WPF's dispatcher pumps the messages it needs.
/// </summary>
internal sealed class TrayIcon : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly Icon _icon;
    private readonly nint _hIcon;

    /// <param name="argbPixels">Icon image, 0xAARRGGBB.</param>
    /// <param name="size">Icon width and height.</param>
    /// <param name="menu">Right-click menu (shared with the pet).</param>
    public TrayIcon(uint[] argbPixels, int size, ContextMenuStrip menu)
    {
        (_icon, _hIcon) = CreateIcon(argbPixels, size);
        _notifyIcon = new NotifyIcon
        {
            Icon = _icon,
            Text = "DesktopPet",
            ContextMenuStrip = menu,
            Visible = true,
        };
    }

    /// <summary>A notification from the tray icon (Windows shows it as a toast).</summary>
    public void Notify(string title, string text, bool warning = false) =>
        _notifyIcon.ShowBalloonTip(5000, title, text, warning ? ToolTipIcon.Warning : ToolTipIcon.Info);

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.ContextMenuStrip = null; // owned by PetMenu
        _notifyIcon.Dispose();
        _icon.Dispose();
        NativeIcon.Destroy(_hIcon); // Icon.FromHandle doesn't own the handle
    }

    private static (Icon Icon, nint Handle) CreateIcon(uint[] argb, int size)
    {
        using var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        var data = bitmap.LockBits(new Rectangle(0, 0, size, size), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try
        {
            // Format32bppArgb is laid out as B,G,R,A bytes, same as our packed 0xAARRGGBB uints.
            var ints = MemoryMarshal.Cast<uint, int>(argb).ToArray();
            for (var row = 0; row < size; row++)
            {
                Marshal.Copy(ints, row * size, data.Scan0 + (row * data.Stride), size);
            }
        }
        finally
        {
            bitmap.UnlockBits(data);
        }

        var handle = bitmap.GetHicon();
        return (Icon.FromHandle(handle), handle);
    }
}
