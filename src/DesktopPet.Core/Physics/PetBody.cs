using DesktopPet.Core.Math;

namespace DesktopPet.Core.Physics;

public enum BodyMode
{
    Airborne,
    Grounded,
}

/// <summary>The pet as a physics object: a feet point (bottom-centre) with velocity, in physical pixels.</summary>
public sealed class PetBody
{
    public Vec2 Feet { get; set; }
    public Vec2 Velocity { get; set; }
    public BodyMode Mode { get; private set; } = BodyMode.Airborne;

    /// <summary><see cref="World.Platform.Key"/> of the surface under the pet while grounded.</summary>
    public string? GroundKey { get; private set; }

    public void Land(double x, double y, string platformKey)
    {
        Feet = new Vec2(x, y);
        Velocity = Vec2.Zero;
        Mode = BodyMode.Grounded;
        GroundKey = platformKey;
    }

    public void Release(Vec2 velocity)
    {
        Velocity = velocity;
        Mode = BodyMode.Airborne;
        GroundKey = null;
    }
}
