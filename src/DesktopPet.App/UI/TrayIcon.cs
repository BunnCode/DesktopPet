using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using DesktopPet.Platform.Windows;

namespace DesktopPet.App.UI;

/// <summary>
/// Notification-area icon. Essential: the pet has no taskbar button or Alt-Tab entry, so this is how you quit.
/// Uses WinForms' NotifyIcon; WPF's dispatcher pumps the messages it needs.
/// </summary>
internal sealed class TrayIcon : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly Icon _icon;
    private readonly nint _hIcon;

    public TrayIcon(uint[] argbPixels, int size, Action onExit)
    {
        (_icon, _hIcon) = CreateIcon(argbPixels, size);

        var menu = new ContextMenuStrip();
        menu.Items.Add(new ToolStripMenuItem("DesktopPet") { Enabled = false });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", image: null, (_, _) => onExit());

        _notifyIcon = new NotifyIcon
        {
            Icon = _icon,
            Text = "DesktopPet",
            ContextMenuStrip = menu,
            Visible = true,
        };
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.ContextMenuStrip?.Dispose();
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
