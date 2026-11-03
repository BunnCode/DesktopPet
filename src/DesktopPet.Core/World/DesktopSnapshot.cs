namespace DesktopPet.Core.World;

/// <summary>
/// Everything Core knows about the desktop at one moment. Produced by the platform layer (or a test fake);
/// Core never queries the OS itself. Other top-level windows join this in M5.
/// </summary>
public sealed record DesktopSnapshot(IReadOnlyList<MonitorInfo> Monitors)
{
    public static DesktopSnapshot Empty { get; } = new([]);
}
