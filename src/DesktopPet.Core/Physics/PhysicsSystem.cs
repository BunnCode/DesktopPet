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

    /// <summary>Caught a hang bar while moving up through it.</summary>
    Grabbed,

    /// <summary>Flew into a wall slowly enough to grab on.</summary>
    Clung,
}

/// <summary>What the physics step needs to know about the world and the pet's current size.</summary>
/// <param name="Platforms">Surfaces to land on.</param>
/// <param name="Monitors">Monitor layout (for walls).</param>
/// <param name="Scale">DPI scale where the pet is; DIP-based constants are multiplied by it.</param>
/// <param name="Walls">Climbable walls (window sides, screen edges); empty disables clinging.</param>
public readonly record struct PhysicsWorld(
    IReadOnlyList<Platform> Platforms, IReadOnlyList<MonitorInfo> Monitors, double Scale, IReadOnlyList<Wall>? Walls = null)
{
    public IReadOnlyList<Wall> WallsOrEmpty => Walls ?? [];
}

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
            BodyMode.Hanging => StepHanging(body, world, config),
            BodyMode.Clinging => StepClinging(body, world, config),
            _ => PhysicsEvent.None, // Held: the mouse owns it
        };
    }

    /// <summary>
    /// Keeps a clinging body on its wall: rides a window that's been moved (not resized), lets go when the wall
    /// disappears or the body is no longer beside it.
    /// </summary>
    private static PhysicsEvent StepClinging(PetBody body, in PhysicsWorld world, PetConfig config)
    {
        var size = config.SizeDip * world.Scale;
        var owner = body.WallOwner;
        var feet = body.Feet;

        if (FindWall(world.WallsOrEmpty, body.GroundKey, feet.Y - (size / 2)) is { } probe && !owner.IsEmpty)
        {
            var now = probe.Owner;
            var dx = now.Width == owner.Width ? now.Left - owner.Left : 0;
            var dy = now.Height == owner.Height ? now.Top - owner.Top : 0;
            feet += new Vec2(dx, dy);
        }

        if (FindWall(world.WallsOrEmpty, body.GroundKey, feet.Y - (size / 2)) is not { } wall || !wall.SpansY(feet.Y - (size / 2)))
        {
            body.Feet = feet;
            body.Release(Vec2.Zero);
            return PhysicsEvent.LostGround;
        }

        body.Cling(wall, feet.Y, size);
        return PhysicsEvent.None;
    }

    /// <summary>A wall by key, preferring the segment spanning <paramref name="y"/> when it's split.</summary>
    public static Wall? FindWall(IReadOnlyList<Wall> walls, string? key, double y)
    {
        ArgumentNullException.ThrowIfNull(walls);
        if (key is null)
        {
            return null;
        }

        Wall? any = null;
        foreach (var w in walls)
        {
            if (w.Key != key)
            {
                continue;
            }

            if (w.SpansY(y))
            {
                return w;
            }

            any ??= w;
        }

        return any;
    }

    /// <summary>
    /// A wall the pet's side would touch moving from <paramref name="fromX"/> to <paramref name="toX"/> (feet x),
    /// facing it, with its mid-body height inside the wall's span.
    /// </summary>
    public static Wall? FindWallCrossing(IReadOnlyList<Wall> walls, double fromX, double toX, double midY, double halfWidth)
    {
        ArgumentNullException.ThrowIfNull(walls);
        var direction = System.Math.Sign(toX - fromX);
        if (direction == 0)
        {
            return null;
        }

        Wall? best = null;
        var fromEdge = fromX + (direction * halfWidth);
        var toEdge = toX + (direction * halfWidth);
        foreach (var w in walls)
        {
            if (w.TowardWall != direction || !w.SpansY(midY))
            {
                continue;
            }

            var crosses = direction > 0 ? fromEdge <= w.X && toEdge >= w.X : fromEdge >= w.X && toEdge <= w.X;
            if (crosses && (best is null || System.Math.Abs(w.X - fromEdge) < System.Math.Abs(best.Value.X - fromEdge)))
            {
                best = w;
            }
        }

        return best;
    }

    /// <summary>Keeps a hanging body on its bar as the window moves; lets go when the bar goes away or ends.</summary>
    private static PhysicsEvent StepHanging(PetBody body, in PhysicsWorld world, PetConfig config)
    {
        if (FindGround(world.Platforms, body.GroundKey, body.Feet.X) is not { } bar)
        {
            body.Release(Vec2.Zero);
            return PhysicsEvent.LostGround;
        }

        if (bar.AnchorX != body.GroundAnchorX && bar.AnchorWidth == body.GroundAnchorWidth)
        {
            body.Feet = body.Feet with { X = body.Feet.X + (bar.AnchorX - body.GroundAnchorX) };
        }

        if (!bar.SpansX(body.Feet.X))
        {
            body.Release(Vec2.Zero);
            return PhysicsEvent.LostGround;
        }

        body.Hang(body.Feet.X, bar, config.SizeDip * world.Scale);
        return PhysicsEvent.None;
    }

    /// <summary>
    /// The hang bar a pet with feet at <paramref name="feet"/> can reach with its hands: the top of its sprite within
    /// <paramref name="reach"/> pixels of a bar spanning its x.
    /// </summary>
    public static Platform? FindBarInReach(IReadOnlyList<Platform> platforms, Vec2 feet, double bodyHeight, double reach)
    {
        ArgumentNullException.ThrowIfNull(platforms);
        var head = feet.Y - bodyHeight;
        Platform? best = null;
        foreach (var p in platforms)
        {
            if (p.Kind == PlatformKind.HangBar && p.SpansX(feet.X) && System.Math.Abs(p.Y - head) <= reach
                && (best is null || System.Math.Abs(p.Y - head) < System.Math.Abs(best.Value.Y - head)))
            {
                best = p;
            }
        }

        return best;
    }

    private static PhysicsEvent StepGrounded(PetBody body, in PhysicsWorld world, double dt, PetConfig config)
    {
        if (FindGround(world.Platforms, body.GroundKey, body.Feet.X) is not { } g)
        {
            // Surface vanished: window closed, minimized, maximized, moved to another desktop, monitor unplugged...
            body.Release(new Vec2(body.Velocity.X, 0));
            return PhysicsEvent.LostGround;
        }

        // Ride: if the surface's owner moved sideways (same width, so moved rather than resized), move with it.
        if (g.AnchorX != body.GroundAnchorX && g.AnchorWidth == body.GroundAnchorWidth)
        {
            body.Feet = body.Feet with { X = body.Feet.X + (g.AnchorX - body.GroundAnchorX) };
        }

        // Floors that drop away (taskbar shrank) are fallen to; windows are followed down too (being dragged).
        if (g.Kind == PlatformKind.Floor && g.Y > body.Feet.Y + SnapDownTolerance)
        {
            body.Release(new Vec2(body.Velocity.X, 0));
            return PhysicsEvent.LostGround;
        }

        body.TrackGround(g);
        var skidded = Skid(body, world, dt, config);

        if (!g.SpansX(body.Feet.X))
        {
            // Walked/slid/rode off this segment. Another visible segment of the same surface, or a neighbouring
            // surface at the same height (e.g. the next monitor's floor), takes over; otherwise fall.
            if (FindGround(world.Platforms, body.GroundKey, body.Feet.X) is { } same && same.SpansX(body.Feet.X))
            {
                g = same;
            }
            else if (FindAdjacent(world.Platforms, body.Feet.X, g.Y) is { } next)
            {
                body.Land(body.Feet.X, next, body.Velocity.X);
                return skidded;
            }
            else
            {
                body.Release(new Vec2(body.Velocity.X, 0));
                return PhysicsEvent.LostGround;
            }
        }

        body.Feet = body.Feet with { Y = g.Y };
        body.TrackGround(g);
        return skidded;
    }

    /// <summary>The ground platform by key, preferring the segment under <paramref name="x"/> when a surface is split.</summary>
    private static Platform? FindGround(IReadOnlyList<Platform> platforms, string? key, double x)
    {
        if (key is null)
        {
            return null;
        }

        Platform? any = null;
        foreach (var p in platforms)
        {
            if (p.Key != key)
            {
                continue;
            }

            if (p.SpansX(x))
            {
                return p;
            }

            any ??= p;
        }

        return any;
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

        // Flying into a wall it's facing: grab on if slow enough. (Too fast: screen edges bounce below, window sides are passed.)
        if (config.EnableClimbing && velocity.X != 0 && System.Math.Abs(velocity.X) <= config.ClingMaxSpeedDip * scale)
        {
            var size = config.SizeDip * scale;
            if (FindWallCrossing(world.WallsOrEmpty, from.X, to.X, to.Y - (size / 2), HalfWidth(world, config)) is { } wall)
            {
                body.Cling(wall, to.Y, size);
                return PhysicsEvent.Clung;
            }
        }

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
            body.Land(landing.X, landing.Platform, velocity.X);
            return PhysicsEvent.Landed;
        }

        // Thrown upward: hands catch a hang bar as the head passes up through it.
        var height = config.SizeDip * scale;
        if (velocity.Y < 0 && FindCatch(world.Platforms, from, to, height) is { } caught)
        {
            body.Hang(caught.X, caught.Bar, height);
            return PhysicsEvent.Grabbed;
        }

        body.Feet = to;
        body.Velocity = velocity;
        return result;
    }

    private static double HalfWidth(in PhysicsWorld world, PetConfig config) =>
        config.SizeDip * world.Scale * config.BodyHalfWidthFraction;

    /// <summary>Swept test for the head moving up through a hang bar: the lowest bar crossed, at the x where it's crossed.</summary>
    private static (Platform Bar, double X)? FindCatch(IReadOnlyList<Platform> platforms, Vec2 from, Vec2 to, double height)
    {
        (Platform Bar, double X)? best = null;
        double headFrom = from.Y - height, headTo = to.Y - height, dy = headTo - headFrom;

        foreach (var p in platforms)
        {
            if (p.Kind != PlatformKind.HangBar || headFrom < p.Y || headTo > p.Y)
            {
                continue;
            }

            var t = dy < 0 ? (p.Y - headFrom) / dy : 0;
            var x = from.X + ((to.X - from.X) * t);
            if (p.SpansX(x) && (best is null || p.Y > best.Value.Bar.Y))
            {
                best = (p, x);
            }
        }

        return best;
    }

    private static Platform? FindAdjacent(IReadOnlyList<Platform> platforms, double x, double y)
    {
        foreach (var p in platforms)
        {
            if (p.IsGround && p.SpansX(x) && System.Math.Abs(p.Y - y) <= SnapDownTolerance)
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
            if (!p.IsGround || from.Y > p.Y || to.Y < p.Y)
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
