namespace DesktopPet.Core.World;

/// <summary>Derives walkable surfaces from a desktop snapshot. Pure geometry.</summary>
public static class PlatformBuilder
{
    public static IReadOnlyList<Platform> Build(DesktopSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return snapshot.Monitors
            .Select(m => new Platform(PlatformKind.Floor, FloorKey(m), m.WorkArea.Bottom, m.WorkArea.Left, m.WorkArea.Right))
            .ToArray();
    }

    public static string FloorKey(MonitorInfo monitor)
    {
        ArgumentNullException.ThrowIfNull(monitor);
        return "floor:" + monitor.Id;
    }
}
