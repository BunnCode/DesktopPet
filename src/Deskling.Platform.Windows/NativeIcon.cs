using Windows.Win32;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Deskling.Platform.Windows;

public static class NativeIcon
{
    /// <summary>Frees an HICON created with e.g. <c>Bitmap.GetHicon()</c>.</summary>
    public static void Destroy(nint hIcon)
    {
        if (hIcon != 0)
        {
            PInvoke.DestroyIcon(new HICON(hIcon));
        }
    }
}
