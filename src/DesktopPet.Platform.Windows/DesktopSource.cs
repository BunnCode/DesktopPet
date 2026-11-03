using System.Diagnostics;
using DesktopPet.Core.World;

namespace DesktopPet.Platform.Windows;

/// <summary>
/// Produces <see cref="DesktopSnapshot"/>s for Core, throttling OS queries. Monitors and work areas are polled
/// about once a second, which also covers taskbar moves, Explorer restarts and display changes.
/// While nothing changes, the same snapshot instance is returned so Core can cache derived data by reference.
/// </summary>
public sealed class DesktopSource
{
    private static readonly TimeSpan MonitorPollInterval = TimeSpan.FromSeconds(1);

    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private TimeSpan _lastMonitorPoll;
    private DesktopSnapshot _current;

    public DesktopSource()
    {
        _current = new DesktopSnapshot(MonitorSource.GetMonitors());
        _lastMonitorPoll = _clock.Elapsed;
    }

    public DesktopSnapshot GetSnapshot()
    {
        var now = _clock.Elapsed;
        if (now - _lastMonitorPoll >= MonitorPollInterval)
        {
            _lastMonitorPoll = now;
            var monitors = MonitorSource.GetMonitors();
            if (!monitors.SequenceEqual(_current.Monitors))
            {
                _current = new DesktopSnapshot(monitors);
            }
        }

        return _current;
    }
}
