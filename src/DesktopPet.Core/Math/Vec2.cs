namespace DesktopPet.Core.Math;

/// <summary>Continuous position/velocity in physical virtual-screen pixels. Y grows downward.</summary>
public readonly record struct Vec2(double X, double Y)
{
    public static readonly Vec2 Zero = new(0, 0);

    public static Vec2 operator +(Vec2 a, Vec2 b) => new(a.X + b.X, a.Y + b.Y);
    public static Vec2 operator -(Vec2 a, Vec2 b) => new(a.X - b.X, a.Y - b.Y);
    public static Vec2 operator -(Vec2 a) => new(-a.X, -a.Y);
    public static Vec2 operator *(Vec2 a, double s) => new(a.X * s, a.Y * s);
    public static Vec2 operator *(double s, Vec2 a) => a * s;
    public static Vec2 operator /(Vec2 a, double s) => new(a.X / s, a.Y / s);

    public double LengthSquared => (X * X) + (Y * Y);
    public double Length => System.Math.Sqrt(LengthSquared);

    public static Vec2 Lerp(Vec2 a, Vec2 b, double t) => a + ((b - a) * t);

    public Vec2I Round() => new((int)System.Math.Round(X), (int)System.Math.Round(Y));
}

/// <summary>Integer point in physical virtual-screen pixels (what Win32 reports).</summary>
public readonly record struct Vec2I(int X, int Y)
{
    public static readonly Vec2I Zero = new(0, 0);

    public Vec2 ToVec2() => new(X, Y);

    public static implicit operator Vec2(Vec2I v) => v.ToVec2();
}
