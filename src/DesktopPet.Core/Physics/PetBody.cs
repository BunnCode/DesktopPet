using DesktopPet.Core.Math;

namespace DesktopPet.Core.Physics;

public enum BodyMode
{
    Airborne,
    Grounded,

    /// <summary>Being dragged by the mouse; physics leaves it alone.</summary>
    Held,
}

/// <summary>The pet as a physics object: a feet point (bottom-centre) with velocity, in physical pixels.</summary>
public sealed class PetBody
{
    public Vec2 Feet { get; set; }
    public Vec2 Velocity { get; set; }
    public BodyMode Mode { get; private set; } = BodyMode.Airborne;

    /// <summary><see cref="World.Platform.Key"/> of the surface under the pet while grounded.</summary>
    public string? GroundKey { get; private set; }

    /// <summary>Touch down on a platform. Horizontal speed is kept so a thrown pet can skid.</summary>
    public void Land(double x, double y, string platformKey, double skidVelocityX = 0)
    {
        Feet = new Vec2(x, y);
        Velocity = new Vec2(skidVelocityX, 0);
        Mode = BodyMode.Grounded;
        GroundKey = platformKey;
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
