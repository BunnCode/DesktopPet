using DesktopPet.Core.Math;
using DesktopPet.Core.Simulation;
using DesktopPet.Core.World;

namespace DesktopPet.Core.Physics;

public enum PhysicsEvent
{
    None,
    Landed,
    LostGround,
    HitWall,
}

/// <summary>What the physics step needs to know about the world and the pet's current size.</summary>
/// <param name="Platforms">Surfaces to land on.</param>
/// <param name="Monitors">Monitor layout (for walls).</param>
/// <param name="Scale">DPI scale where the pet is; DIP-based constants are multiplied by it.</param>
public readonly record struct PhysicsWorld(IReadOnlyList<Platform> Platforms, IReadOnlyList<MonitorInfo> Monitors, double Scale);

/// <summary>
/// Gravity, swept one-way platform collision, wall bounces, ground tracking and skidding.
/// Stateless; operates on a <see cref="PetBody"/>.
/// </summary>
public static class PhysicsSystem
{
    /// <summary>A floor this far below the feet (e.g. the taskbar shrank) makes the pet fall to it rather than snap.</summary>
    private const double SnapDownTolerance = 1.0;

    public static PhysicsEvent Step(PetBody body, in PhysicsWorld world, double dt, PetConfig config)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(config);

        return body.Mode switch
        {
            BodyMode.Grounded => StepGrounded(body, world, dt, config),
            BodyMode.Airborne => StepAirborne(body, world, dt, config),
            _ => PhysicsEvent.None, // Held: the mouse owns it
        };
    }

    private static PhysicsEvent StepGrounded(PetBody body, in PhysicsWorld world, double dt, PetConfig config)
    {
        var skidded = Skid(body, world, dt, config);

        Platform? ground = null;
        foreach (var p in world.Platforms)
        {
            if (p.Key == body.GroundKey)
            {
                ground = p;
                break;
            }
        }

        if (ground is not { } g || g.Y > body.Feet.Y + SnapDownTolerance)
        {
            body.Release(new Vec2(body.Velocity.X, 0));
            return PhysicsEvent.LostGround;
        }

        if (!g.SpansX(body.Feet.X))
        {
            // Walked or slid off the end. Hand over to a neighbouring surface at the same height (e.g. the next monitor's floor).
            if (FindAdjacent(world.Platforms, body.Feet.X, g.Y) is { } next)
            {
                body.Land(body.Feet.X, next.Y, next.Key, body.Velocity.X);
                return skidded;
            }

            body.Release(new Vec2(body.Velocity.X, 0));
            return PhysicsEvent.LostGround;
        }

        // Surface moved up (or by less than the tolerance): stay on it.
        body.Feet = body.Feet with { Y = g.Y };
        return skidded;
    }

    /// <summary>Slides a landed body with leftover horizontal speed, decelerating by friction and stopping at walls.</summary>
    private static PhysicsEvent Skid(PetBody body, in PhysicsWorld world, double dt, PetConfig config)
    {
        var vx = body.Velocity.X;
        if (vx == 0)
        {
            return PhysicsEvent.None;
        }

        var decel = config.GroundFrictionDip * world.Scale * dt;
        var newVx = System.Math.Abs(vx) <= decel ? 0 : vx - (System.Math.Sign(vx) * decel);
        var dx = (vx + newVx) / 2 * dt;

        var leadingEdge = body.Feet.X + (System.Math.Sign(vx) * HalfWidth(world, config)) + dx;
        if (!WorldQuery.IsWalkable(world.Monitors, leadingEdge, body.Feet.Y))
        {
            body.Velocity = Vec2.Zero;
            return PhysicsEvent.HitWall;
        }

        body.Feet = body.Feet with { X = body.Feet.X + dx };
        body.Velocity = new Vec2(newVx, 0);
        return PhysicsEvent.None;
    }

    private static PhysicsEvent StepAirborne(PetBody body, in PhysicsWorld world, double dt, PetConfig config)
    {
        var scale = world.Scale;
        var velocity = body.Velocity;

        // Air drag on horizontal motion only, so falls stay crisp.
        velocity = velocity with { X = velocity.X * System.Math.Exp(-config.AirDragPerSecond * dt) };
        velocity += new Vec2(0, config.GravityDip * scale * dt);

        var terminal = config.TerminalVelocityDip * scale;
        if (velocity.Y > terminal)
        {
            velocity = velocity with { Y = terminal };
        }

        var from = body.Feet;
        var to = from + (velocity * dt);
        var result = PhysicsEvent.None;

        // Walls: bounce if the leading edge, at mid-body height, would end up somewhere unreachable.
        if (velocity.X != 0)
        {
            var leading = new Vec2(
                to.X + (System.Math.Sign(velocity.X) * HalfWidth(world, config)),
                to.Y - (config.SizeDip * scale / 2));
            if (WorldQuery.IsLost(world.Monitors, leading))
            {
                velocity = velocity with { X = -velocity.X * config.WallRestitution };
                to = to with { X = from.X };
                result = PhysicsEvent.HitWall;
            }
        }

        if (velocity.Y >= 0 && FindLanding(world.Platforms, from, to) is { } landing)
        {
            body.Land(landing.X, landing.Platform.Y, landing.Platform.Key, velocity.X);
            return PhysicsEvent.Landed;
        }

        body.Feet = to;
        body.Velocity = velocity;
        return result;
    }

    private static double HalfWidth(in PhysicsWorld world, PetConfig config) =>
        config.SizeDip * world.Scale * config.BodyHalfWidthFraction;

    private static Platform? FindAdjacent(IReadOnlyList<Platform> platforms, double x, double y)
    {
        foreach (var p in platforms)
        {
            if (p.SpansX(x) && System.Math.Abs(p.Y - y) <= SnapDownTolerance)
            {
                return p;
            }
        }

        return null;
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
