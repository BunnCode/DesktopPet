using DesktopPet.Core.Math;
using DesktopPet.Core.World;

namespace DesktopPet.Core.Physics;

public enum BodyMode
{
    Airborne,
    Grounded,

    /// <summary>Being dragged by the mouse; physics leaves it alone.</summary>
    Held,

    /// <summary>Hanging by its hands from a <see cref="PlatformKind.HangBar"/>; the top of the sprite is at the bar.</summary>
    Hanging,
}

/// <summary>The pet as a physics object: a feet point (bottom-centre) with velocity, in physical pixels.</summary>
public sealed class PetBody
{
    public Vec2 Feet { get; set; }
    public Vec2 Velocity { get; set; }
    public BodyMode Mode { get; private set; } = BodyMode.Airborne;

    /// <summary><see cref="Platform.Key"/> of the surface under the pet while grounded.</summary>
    public string? GroundKey { get; private set; }

    /// <summary><see cref="Platform.AnchorX"/> of the ground when last seen.</summary>
    public double GroundAnchorX { get; private set; }

    /// <summary><see cref="Platform.AnchorWidth"/> of the ground when last seen.</summary>
    public double GroundAnchorWidth { get; private set; }

    /// <summary>Touch down on a platform. Horizontal speed is kept so a thrown pet can skid.</summary>
    public void Land(double x, in Platform platform, double skidVelocityX = 0)
    {
        Feet = new Vec2(x, platform.Y);
        Velocity = new Vec2(skidVelocityX, 0);
        Mode = BodyMode.Grounded;
        TrackGround(platform);
    }

    /// <summary>Grab a hang bar. <paramref name="bodyHeight"/> puts the feet that far below the bar (hands at the bar).</summary>
    public void Hang(double x, in Platform bar, double bodyHeight)
    {
        Feet = new Vec2(x, bar.Y + bodyHeight);
        Velocity = Vec2.Zero;
        Mode = BodyMode.Hanging;
        TrackGround(bar);
    }

    /// <summary>Notes the ground's current anchor after following it.</summary>
    public void TrackGround(in Platform platform)
    {
        GroundKey = platform.Key;
        GroundAnchorX = platform.AnchorX;
        GroundAnchorWidth = platform.AnchorWidth;
    }

    public void Release(Vec2 velocity)
    {
        Velocity = velocity;
        Mode = BodyMode.Airborne;
        GroundKey = null;
    }

    public void Grab()
    {
        Velocity = Vec2.Zero;
        Mode = BodyMode.Held;
        GroundKey = null;
    }
}
