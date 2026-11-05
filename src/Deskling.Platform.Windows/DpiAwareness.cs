using Windows.Win32;
using Windows.Win32.UI.HiDpi;

namespace Deskling.Platform.Windows;

/// <summary>
/// Makes the process PerMonitorV2 DPI aware, so Win32 coordinates are physical pixels on every monitor.
/// app.manifest already asks for it, but a manifest only applies when the app is started from Deskling.App.exe:
/// started as <c>dotnet Deskling.App.dll</c> (as some debuggers do), the process gets dotnet.exe's manifest
/// and Windows scales coordinates on monitors whose scale differs from the primary's.
/// </summary>
public static class DpiAwareness
{
    /// <summary>DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 (a pseudo-handle; CsWin32 doesn't generate it).</summary>
    private static readonly DPI_AWARENESS_CONTEXT PerMonitorV2 = (DPI_AWARENESS_CONTEXT)(nint)(-4);

    /// <summary>
    /// Asks for PerMonitorV2 unless the process already has it. Call before any window is created.
    /// Returns whether the process is PerMonitorV2 aware afterwards.
    /// </summary>
    public static bool EnsurePerMonitorV2()
    {
        // PerMonitorV2 needs Windows 10 1703+.
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 15063))
        {
            return false;
        }

        if (IsPerMonitorV2())
        {
            return true;
        }

        _ = PInvoke.SetProcessDpiAwarenessContext(PerMonitorV2);
        return IsPerMonitorV2();
    }

    private static bool IsPerMonitorV2() =>
        OperatingSystem.IsWindowsVersionAtLeast(10, 0, 15063) &&
        PInvoke.AreDpiAwarenessContextsEqual(PInvoke.GetThreadDpiAwarenessContext(), PerMonitorV2);
}
