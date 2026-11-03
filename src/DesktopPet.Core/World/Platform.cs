namespace DesktopPet.Core.World;

public enum PlatformKind
{
    /// <summary>A monitor's work-area bottom (the taskbar top, or the screen bottom).</summary>
    Floor,
}

/// <summary>
/// A one-way horizontal surface at <see cref="Y"/>, spanning [<see cref="Left"/>, <see cref="Right"/>).
/// Only collides with a body moving downward through it.
/// </summary>
/// <param name="Kind">What the surface is.</param>
/// <param name="Key">Stable identity across snapshots, so a grounded body can find "its" platform again.</param>
/// <param name="Y">Height of the surface in physical pixels.</param>
/// <param name="Left">Inclusive left end.</param>
/// <param name="Right">Exclusive right end.</param>
public readonly record struct Platform(PlatformKind Kind, string Key, double Y, double Left, double Right)
{
    public bool SpansX(double x) => x >= Left && x < Right;
}
