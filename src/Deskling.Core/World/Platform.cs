namespace Deskling.Core.World;

public enum PlatformKind
{
    /// <summary>A monitor's work-area bottom (the taskbar top, or the screen bottom).</summary>
    Floor,

    /// <summary>The visible part of a window's top edge.</summary>
    WindowTop,

    /// <summary>
    /// The bottom edge of the title bar of a window whose top can't be stood on (maximized, or pinned to the screen top).
    /// Not ground: the pet hangs from it by its hands, body below.
    /// </summary>
    HangBar,
}

/// <summary>
/// A one-way horizontal surface at <see cref="Y"/>, spanning [<see cref="Left"/>, <see cref="Right"/>).
/// Only collides with a body moving downward through it.
/// </summary>
/// <param name="Kind">What the surface is.</param>
/// <param name="Key">
/// Stable identity across snapshots, so a grounded body can find "its" surface again. A partly covered window
/// yields several segments sharing one key.
/// </param>
/// <param name="Y">Height of the surface in physical pixels.</param>
/// <param name="Left">Inclusive left end.</param>
/// <param name="Right">Exclusive right end.</param>
/// <param name="AnchorX">
/// X of the thing the surface belongs to (a window's left edge). When it changes between snapshots while
/// <paramref name="AnchorWidth"/> stays the same, the owner was moved and a body standing on it rides along.
/// </param>
/// <param name="AnchorWidth">Width of the owner; a change means it was resized, not moved, so riders stay put.</param>
public readonly record struct Platform(
    PlatformKind Kind, string Key, double Y, double Left, double Right, double AnchorX = 0, double AnchorWidth = 0)
{
    public bool SpansX(double x) => x >= Left && x < Right;

    /// <summary>Whether a falling body can land on it (everything but hang bars).</summary>
    public bool IsGround => Kind != PlatformKind.HangBar;

    public double Width => Right - Left;
}
