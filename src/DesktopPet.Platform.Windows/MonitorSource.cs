using DesktopPet.Core.Math;
using DesktopPet.Core.World;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.HiDpi;

namespace DesktopPet.Platform.Windows;

/// <summary>Enumerates displays in physical pixels (requires a PerMonitorV2-aware process).</summary>
public static class MonitorSource
{
    public static unsafe IReadOnlyList<MonitorInfo> GetMonitors()
    {
        var monitors = new List<MonitorInfo>();

        BOOL Collect(HMONITOR handle, HDC dc, RECT* clip, LPARAM data)
        {
            var info = new MONITORINFOEXW();
            info.monitorInfo.cbSize = (uint)sizeof(MONITORINFOEXW);
            if (PInvoke.GetMonitorInfo(handle, (MONITORINFO*)&info))
            {
                var scale = 1.0;
                // PerMonitorV2 (which this app requires) needs Windows 10 1703+.
                if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 15063) &&
                    PInvoke.GetDpiForMonitor(handle, MONITOR_DPI_TYPE.MDT_EFFECTIVE_DPI, out var dpiX, out _).Succeeded)
                {
                    scale = dpiX / 96.0;
                }

                monitors.Add(new MonitorInfo(
                    Id: info.szDevice.ToString(),
                    Bounds: ToRect(info.monitorInfo.rcMonitor),
                    WorkArea: ToRect(info.monitorInfo.rcWork),
                    Scale: scale,
                    IsPrimary: (info.monitorInfo.dwFlags & PInvoke.MONITORINFOF_PRIMARY) != 0));
            }

            return true; // continue enumeration
        }

        PInvoke.EnumDisplayMonitors(HDC.Null, (RECT?)null, Collect, default);
        return monitors;
    }

    internal static RectI ToRect(RECT r) => new(r.left, r.top, r.right, r.bottom);
}
