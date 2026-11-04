using System.Diagnostics;
using System.Runtime.InteropServices.WindowsRuntime;
using DesktopPet.Core.Chat;
using DesktopPet.Core.Math;
using global::Windows.Graphics.Imaging;
using global::Windows.Media.Ocr;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;

namespace DesktopPet.Platform.Windows;

/// <summary>Facts about another window for deciding whether it may be read: its title and its app's process name.</summary>
public static class WindowIdentity
{
    public static unsafe (string Title, string ProcessName) Describe(long windowId)
    {
        var hwnd = new HWND((nint)windowId);
        const int maxTitle = 512;
        var buffer = stackalloc char[maxTitle];
        var length = PInvoke.GetWindowText(hwnd, buffer, maxTitle);
        var title = length > 0 ? new string(buffer, 0, length) : string.Empty;

        uint processId;
        _ = PInvoke.GetWindowThreadProcessId(hwnd, &processId);
        string process;
        try
        {
            using var p = Process.GetProcessById((int)processId);
            process = p.ProcessName;
        }
        catch (ArgumentException)
        {
            process = string.Empty; // exited
        }
        catch (InvalidOperationException)
        {
            process = string.Empty;
        }

        return (title, process);
    }

    /// <summary>
    /// The visible frames of other processes' windows in front of <paramref name="windowId"/> (z-order), so text under
    /// them isn't taken for the window's own. Click-through overlays don't count; they show nothing solid.
    /// </summary>
    public static IReadOnlyList<RectI> CoversOf(long windowId, uint excludeProcessId)
    {
        var target = new HWND((nint)windowId);
        var covers = new List<RectI>();
        var reached = false;

        unsafe BOOL Visit(HWND hwnd, LPARAM data)
        {
            if (hwnd == target)
            {
                reached = true;
                return false; // stop: everything after it is behind
            }

            if (!PInvoke.IsWindowVisible(hwnd) || PInvoke.IsIconic(hwnd))
            {
                return true;
            }

            uint processId;
            _ = PInvoke.GetWindowThreadProcessId(hwnd, &processId);
            var exStyle = (WINDOW_EX_STYLE)PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
            int cloaked = 0;
            if (processId == excludeProcessId || (exStyle & WINDOW_EX_STYLE.WS_EX_TRANSPARENT) != 0 ||
                (PInvoke.DwmGetWindowAttribute(hwnd, DWMWINDOWATTRIBUTE.DWMWA_CLOAKED, &cloaked, sizeof(int)).Succeeded && cloaked != 0))
            {
                return true;
            }

            RECT rect;
            if (PInvoke.DwmGetWindowAttribute(hwnd, DWMWINDOWATTRIBUTE.DWMWA_EXTENDED_FRAME_BOUNDS, &rect, (uint)sizeof(RECT)).Failed &&
                !PInvoke.GetWindowRect(hwnd, out rect))
            {
                return true;
            }

            var bounds = MonitorSource.ToRect(rect);
            if (!bounds.IsEmpty)
            {
                covers.Add(bounds);
            }

            return true;
        }

        PInvoke.EnumWindows(Visit, default);
        return reached ? covers : [];
    }
}

/// <summary>Copies part of the screen as it's shown (physical pixels).</summary>
public static class ScreenCapture
{
    /// <summary>The pixels of <paramref name="area"/> as 32-bit BGRA rows, top to bottom; null if the capture failed.</summary>
    public static unsafe byte[]? Capture(RectI area)
    {
        if (area.IsEmpty)
        {
            return null;
        }

        var screen = PInvoke.GetDC(HWND.Null);
        if (screen.IsNull)
        {
            return null;
        }

        var memory = PInvoke.CreateCompatibleDC(screen);
        try
        {
            var info = default(BITMAPINFO);
            info.bmiHeader.biSize = (uint)sizeof(BITMAPINFOHEADER);
            info.bmiHeader.biWidth = area.Width;
            info.bmiHeader.biHeight = -area.Height; // top-down rows
            info.bmiHeader.biPlanes = 1;
            info.bmiHeader.biBitCount = 32;
            info.bmiHeader.biCompression = 0; // BI_RGB

            void* bits;
            var bitmap = PInvoke.CreateDIBSection(memory, &info, DIB_USAGE.DIB_RGB_COLORS, &bits, HANDLE.Null, 0);
            if (bitmap.IsNull)
            {
                return null;
            }

            try
            {
                var old = PInvoke.SelectObject(memory, bitmap);
                var copied = PInvoke.BitBlt(memory, 0, 0, area.Width, area.Height, screen, area.Left, area.Top, ROP_CODE.SRCCOPY);
                PInvoke.SelectObject(memory, old);
                if (!copied)
                {
                    return null;
                }

                var pixels = new byte[area.Width * area.Height * 4];
                new ReadOnlySpan<byte>(bits, pixels.Length).CopyTo(pixels);
                return pixels;
            }
            finally
            {
                PInvoke.DeleteObject(bitmap);
            }
        }
        finally
        {
            PInvoke.DeleteDC(memory);
            _ = PInvoke.ReleaseDC(HWND.Null, screen);
        }
    }
}

/// <summary>The text recognition (OCR) built into Windows, for the user's languages.</summary>
public sealed class TextRecognizer
{
    /// <summary>Below this DPI scale, captures are enlarged 2× first: Windows' OCR misses small text.</summary>
    private const double EnlargeBelowScale = 1.5;

    private readonly OcrEngine? _engine = OcrEngine.TryCreateFromUserProfileLanguages();

    /// <summary>False when Windows has no OCR for any of the user's languages.</summary>
    public bool Available => _engine is not null;

    /// <summary>
    /// The lines of text in a capture of <paramref name="area"/> (see <see cref="ScreenCapture"/>), with screen
    /// bounds. Runs off the calling thread.
    /// </summary>
    /// <param name="pixels">BGRA rows of the area.</param>
    /// <param name="area">Where on screen they came from.</param>
    /// <param name="scale">The DPI scale there: at 100–125% the capture is enlarged 2× first.</param>
    /// <param name="cancellationToken">Cancels before recognizing.</param>
    public Task<IReadOnlyList<ScreenLine>> RecognizeAsync(byte[] pixels, RectI area, double scale, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        if (_engine is null)
        {
            return Task.FromResult<IReadOnlyList<ScreenLine>>([]);
        }

        return Task.Run(
            async () =>
            {
                var factor = scale < EnlargeBelowScale && System.Math.Max(area.Width, area.Height) * 2 <= OcrEngine.MaxImageDimension ? 2 : 1;
                var (data, width, height) = factor == 2 ? Enlarge(pixels, area.Width, area.Height) : (pixels, area.Width, area.Height);
                if (System.Math.Max(width, height) > OcrEngine.MaxImageDimension)
                {
                    return (IReadOnlyList<ScreenLine>)[];
                }

                cancellationToken.ThrowIfCancellationRequested();
                using var bitmap = SoftwareBitmap.CreateCopyFromBuffer(data.AsBuffer(), BitmapPixelFormat.Bgra8, width, height, BitmapAlphaMode.Ignore);
                var result = await _engine.RecognizeAsync(bitmap);
                var lines = new List<ScreenLine>();
                foreach (var line in result.Lines)
                {
                    if (line.Words.Count == 0)
                    {
                        continue;
                    }

                    double left = double.MaxValue, top = double.MaxValue, right = double.MinValue, bottom = double.MinValue;
                    foreach (var word in line.Words)
                    {
                        var r = word.BoundingRect;
                        left = System.Math.Min(left, r.X);
                        top = System.Math.Min(top, r.Y);
                        right = System.Math.Max(right, r.X + r.Width);
                        bottom = System.Math.Max(bottom, r.Y + r.Height);
                    }

                    var bounds = new RectI(
                        area.Left + (int)(left / factor), area.Top + (int)(top / factor),
                        area.Left + (int)System.Math.Ceiling(right / factor), area.Top + (int)System.Math.Ceiling(bottom / factor));
                    lines.Add(new ScreenLine(line.Text, bounds));
                }

                return lines;
            },
            cancellationToken);
    }

    /// <summary>Doubles the size (each pixel becomes 2×2).</summary>
    private static (byte[] Data, int Width, int Height) Enlarge(byte[] pixels, int width, int height)
    {
        var w2 = width * 2;
        var result = new byte[w2 * height * 2 * 4];
        var source = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, uint>(pixels.AsSpan());
        var target = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, uint>(result.AsSpan());
        for (var y = 0; y < height; y++)
        {
            var row = source.Slice(y * width, width);
            var top = target.Slice(y * 2 * w2, w2);
            for (var x = 0; x < width; x++)
            {
                top[x * 2] = row[x];
                top[(x * 2) + 1] = row[x];
            }

            top.CopyTo(target.Slice(((y * 2) + 1) * w2, w2));
        }

        return (result, w2, height * 2);
    }
}
