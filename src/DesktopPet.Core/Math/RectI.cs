namespace DesktopPet.Core.Math;

/// <summary>
/// Integer rectangle in physical virtual-screen pixels, half-open: [Left, Right) x [Top, Bottom).
/// Matches Win32 RECT semantics. Y grows downward.
/// </summary>
public readonly record struct RectI(int Left, int Top, int Right, int Bottom)
{
    public static RectI FromSize(int x, int y, int width, int height) => new(x, y, x + width, y + height);

    public int Width => Right - Left;
    public int Height => Bottom - Top;
    public bool IsEmpty => Width <= 0 || Height <= 0;

    public bool Contains(Vec2 p) => p.X >= Left && p.X < Right && p.Y >= Top && p.Y < Bottom;

    public bool Intersects(RectI other) =>
        Left < other.Right && other.Left < Right && Top < other.Bottom && other.Top < Bottom;

    /// <summary>Overlap of the two rectangles, or <c>default</c> (empty) if they don't overlap.</summary>
    public RectI Intersect(RectI other)
    {
        var r = new RectI(
            System.Math.Max(Left, other.Left), System.Math.Max(Top, other.Top),
            System.Math.Min(Right, other.Right), System.Math.Min(Bottom, other.Bottom));
        return r.IsEmpty ? default : r;
    }

    public RectI Offset(int dx, int dy) => new(Left + dx, Top + dy, Right + dx, Bottom + dy);
}
