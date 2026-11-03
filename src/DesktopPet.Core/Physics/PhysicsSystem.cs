using DesktopPet.Core.Math;
using DesktopPet.Core.Simulation;
using DesktopPet.Core.World;

namespace DesktopPet.Core.Physics;

public enum PhysicsEvent
{
    None,
    Landed,
    LostGround,
}

/// <summary>Gravity, swept one-way platform collision and ground tracking. Stateless; operates on a <see cref="PetBody"/>.</summary>
public static class PhysicsSystem
{
    /// <summary>A floor this far below the feet (e.g. the taskbar shrank) makes the pet fall to it rather than snap.</summary>
    private const double SnapDownTolerance = 1.0;

    /// <param name="body">Body to advance.</param>
    /// <param name="platforms">Current surfaces.</param>
    /// <param name="dt">Step in seconds.</param>
    /// <param name="scale">DPI scale where the pet is; DIP-based constants are multiplied by it.</param>
    /// <param name="config">Tuning.</param>
    public static PhysicsEvent Step(PetBody body, IReadOnlyList<Platform> platforms, double dt, double scale, PetConfig config)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(platforms);
        ArgumentNullException.ThrowIfNull(config);

        return body.Mode == BodyMode.Grounded
            ? StepGrounded(body, platforms)
            : StepAirborne(body, platforms, dt, scale, config);
    }

    private static PhysicsEvent StepGrounded(PetBody body, IReadOnlyList<Platform> platforms)
    {
        Platform? ground = null;
        foreach (var p in platforms)
        {
            if (p.Key == body.GroundKey)
            {
                ground = p;
                break;
            }
        }

        if (ground is not { } g || !g.SpansX(body.Feet.X) || g.Y > body.Feet.Y + SnapDownTolerance)
        {
            body.Release(Vec2.Zero);
            return PhysicsEvent.LostGround;
        }

        // Surface moved up (or by less than the tolerance): stay on it.
        body.Feet = body.Feet with { Y = g.Y };
        return PhysicsEvent.None;
    }

    private static PhysicsEvent StepAirborne(PetBody body, IReadOnlyList<Platform> platforms, double dt, double scale, PetConfig config)
    {
        var velocity = body.Velocity + new Vec2(0, config.GravityDip * scale * dt);
        var terminal = config.TerminalVelocityDip * scale;
        if (velocity.Y > terminal)
        {
            velocity = velocity with { Y = terminal };
        }

        var from = body.Feet;
        var to = from + (velocity * dt);

        if (velocity.Y >= 0 && FindLanding(platforms, from, to) is { } landing)
        {
            body.Land(landing.X, landing.Platform.Y, landing.Platform.Key);
            return PhysicsEvent.Landed;
        }

        body.Feet = to;
        body.Velocity = velocity;
        return PhysicsEvent.None;
    }

    /// <summary>Swept test: the highest platform the feet segment from→to crosses downward, at the x where it crosses.</summary>
    private static (Platform Platform, double X)? FindLanding(IReadOnlyList<Platform> platforms, Vec2 from, Vec2 to)
    {
        (Platform Platform, double X)? best = null;
        var dy = to.Y - from.Y;

        foreach (var p in platforms)
        {
            if (from.Y > p.Y || to.Y < p.Y)
            {
                continue;
            }

            var t = dy > 0 ? (p.Y - from.Y) / dy : 0;
            var x = from.X + ((to.X - from.X) * t);
            if (p.SpansX(x) && (best is null || p.Y < best.Value.Platform.Y))
            {
                best = (p, x);
            }
        }

        return best;
    }
}
