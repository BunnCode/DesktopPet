using Deskling.Core.Math;

namespace Deskling.Core.World;

public enum WallKind
{
    /// <summary>The left or right side of a window.</summary>
    WindowSide,

    /// <summary>An outer edge of the screen area (not a seam between monitors).</summary>
    ScreenEdge,
}

/// <summary>Which side of the wall line the pet is on while clinging to it.</summary>
public enum WallSide
{
    /// <summary>The pet is left of <see cref="Wall.X"/>, its hands on the wall to its right (e.g. a window's left side).</summary>
    PetOnLeft,

    /// <summary>The pet is right of <see cref="Wall.X"/> (e.g. a window's right side, or the screen's left edge).</summary>
    PetOnRight,
}

/// <summary>
/// A climbable vertical surface at <see cref="X"/>, spanning [<see cref="Top"/>, <see cref="Bottom"/>).
/// A partly covered window side yields several segments sharing one key.
/// </summary>
/// <param name="Kind">Window side or screen edge.</param>
/// <param name="Key">Stable identity across snapshots.</param>
/// <param name="X">The wall line, physical pixels.</param>
/// <param name="Top">Inclusive top.</param>
/// <param name="Bottom">Exclusive bottom.</param>
/// <param name="Side">Which side the pet clings on.</param>
/// <param name="WindowId">Owning window, for window sides.</param>
/// <param name="Owner">Owning window's bounds (for riding and pushing); empty for screen edges.</param>
public readonly record struct Wall(WallKind Kind, string Key, double X, double Top, double Bottom, WallSide Side, long? WindowId, RectI Owner)
{
    public bool SpansY(double y) => y >= Top && y < Bottom;

    /// <summary>+1 if the pet faces right to look at the wall, -1 if left.</summary>
    public int TowardWall => Side == WallSide.PetOnLeft ? 1 : -1;
}
