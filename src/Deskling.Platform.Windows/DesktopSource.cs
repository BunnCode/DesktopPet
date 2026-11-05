using System.Diagnostics;
using Deskling.Core.World;

namespace Deskling.Platform.Windows;

/// <summary>
/// Produces <see cref="DesktopSnapshot"/>s for Core, throttling OS queries:
/// windows about 30 times a second (so a pet riding a dragged window keeps up), monitors and work areas
/// about once a second (also covers taskbar moves, Explorer restarts and display changes).
/// While nothing changes, the same snapshot instance is returned so Core can cache derived data by reference.
/// </summary>
public sealed class DesktopSource
{
    private static readonly TimeSpan MonitorPollInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan WindowPollInterval = TimeSpan.FromMilliseconds(33);

    private readonly uint _ownProcessId = (uint)Environment.ProcessId;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private TimeSpan _lastMonitorPoll;
    private TimeSpan _lastWindowPoll;
    private DesktopSnapshot _current;

    public DesktopSource()
    {
        _current = new DesktopSnapshot(MonitorSource.GetMonitors(), WindowSource.GetWindows(_ownProcessId));
        _lastMonitorPoll = _lastWindowPoll = _clock.Elapsed;
    }

    public DesktopSnapshot GetSnapshot()
    {
        var now = _clock.Elapsed;
        var monitors = _current.Monitors;
        var windows = _current.Windows;

        if (now - _lastMonitorPoll >= MonitorPollInterval)
        {
            _lastMonitorPoll = now;
            var fresh = MonitorSource.GetMonitors();
            if (!fresh.SequenceEqual(monitors))
            {
                monitors = fresh;
            }
        }

        if (now - _lastWindowPoll >= WindowPollInterval)
        {
            _lastWindowPoll = now;
            var fresh = WindowSource.GetWindows(_ownProcessId);
            if (!fresh.SequenceEqual(windows))
            {
                windows = fresh;
            }
        }

        if (!ReferenceEquals(monitors, _current.Monitors) || !ReferenceEquals(windows, _current.Windows))
        {
            _current = new DesktopSnapshot(monitors, windows);
        }

        return _current;
    }
}
