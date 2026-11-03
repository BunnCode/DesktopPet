using DesktopPet.Core.Math;
using DesktopPet.Core.Physics;
using DesktopPet.Core.Simulation;
using DesktopPet.Core.World;

namespace DesktopPet.Core.Tests.Physics;

public class PhysicsSystemTests
{
    private const double Dt = 1.0 / 60.0;
    private static readonly PetConfig Config = PetConfig.Default;
    private static readonly Platform Floor = new(PlatformKind.Floor, "floor", Y: 1000, Left: 0, Right: 1920);

    private static PetBody Airborne(double x, double y, double vx = 0, double vy = 0)
    {
        var body = new PetBody { Feet = new Vec2(x, y) };
        body.Release(new Vec2(vx, vy));
        return body;
    }

    [Fact]
    public void Gravity_accelerates_downward()
    {
        var body = Airborne(100, 0);
        PhysicsSystem.Step(body, [], Dt, 1.0, Config);
        Assert.Equal(Config.GravityDip * Dt, body.Velocity.Y, 6);
        Assert.True(body.Feet.Y > 0);
    }

    [Fact]
    public void Gravity_and_terminal_velocity_scale_with_dpi()
    {
        var body = Airborne(100, 0, vy: 1e6);
        PhysicsSystem.Step(body, [], Dt, 1.5, Config);
        Assert.Equal(Config.TerminalVelocityDip * 1.5, body.Velocity.Y, 6);
    }

    [Fact]
    public void Never_exceeds_terminal_velocity()
    {
        var body = Airborne(100, -1_000_000);
        for (var i = 0; i < 1000; i++)
        {
            PhysicsSystem.Step(body, [], Dt, 1.0, Config);
            Assert.True(body.Velocity.Y <= Config.TerminalVelocityDip + 1e-9);
        }

        Assert.Equal(Config.TerminalVelocityDip, body.Velocity.Y, 6);
    }

    [Fact]
    public void Lands_exactly_on_platform_and_stops()
    {
        var body = Airborne(500, 995, vy: 600);
        var result = PhysicsSystem.Step(body, [Floor], Dt, 1.0, Config);

        Assert.Equal(PhysicsEvent.Landed, result);
        Assert.Equal(BodyMode.Grounded, body.Mode);
        Assert.Equal(new Vec2(500, 1000), body.Feet);
        Assert.Equal(Vec2.Zero, body.Velocity);
        Assert.Equal("floor", body.GroundKey);
    }

    [Fact]
    public void Fast_body_does_not_tunnel_through_thin_platform()
    {
        var body = Airborne(500, 0, vy: 1e7); // would travel ~166k px this step
        var result = PhysicsSystem.Step(body, [Floor], Dt, 1.0, Config with { TerminalVelocityDip = 1e9 });
        Assert.Equal(PhysicsEvent.Landed, result);
        Assert.Equal(1000, body.Feet.Y);
    }

    [Fact]
    public void Lands_on_highest_platform_crossed()
    {
        var upper = new Platform(PlatformKind.Floor, "upper", 600, 0, 1000);
        var body = Airborne(500, 500, vy: 1e6);
        PhysicsSystem.Step(body, [Floor, upper], Dt, 1.0, Config with { TerminalVelocityDip = 1e9 });
        Assert.Equal("upper", body.GroundKey);
    }

    [Fact]
    public void Diagonal_landing_uses_x_at_crossing_point()
    {
        // From (0, 900) to roughly (200, 1100): crosses y=1000 halfway, at x≈100. Platform only spans [50, 150).
        var narrow = new Platform(PlatformKind.Floor, "narrow", 1000, 50, 150);
        var body = Airborne(0, 900, vx: 12000, vy: 12000);
        PhysicsSystem.Step(body, [narrow], Dt, 1.0, Config with { TerminalVelocityDip = 1e9, GravityDip = 0 });
        Assert.Equal(BodyMode.Grounded, body.Mode);
        Assert.Equal(100, body.Feet.X, 1);
    }

    [Fact]
    public void Platforms_are_one_way_moving_up_passes_through()
    {
        var body = Airborne(500, 1010, vy: -6000);
        var result = PhysicsSystem.Step(body, [Floor], Dt, 1.0, Config);
        Assert.Equal(PhysicsEvent.None, result);
        Assert.True(body.Feet.Y < 1000);
        Assert.Equal(BodyMode.Airborne, body.Mode);
    }

    [Fact]
    public void Misses_platform_when_beside_it()
    {
        var body = Airborne(2000, 995, vy: 600);
        Assert.Equal(PhysicsEvent.None, PhysicsSystem.Step(body, [Floor], Dt, 1.0, Config));
        Assert.Equal(BodyMode.Airborne, body.Mode);
    }

    [Fact]
    public void Grounded_follows_platform_that_moves_up()
    {
        var body = new PetBody();
        body.Land(500, 1000, "floor");
        var raised = Floor with { Y = 952 }; // taskbar grew
        Assert.Equal(PhysicsEvent.None, PhysicsSystem.Step(body, [raised], Dt, 1.0, Config));
        Assert.Equal(952, body.Feet.Y);
    }

    [Fact]
    public void Grounded_falls_when_platform_drops_away()
    {
        var body = new PetBody();
        body.Land(500, 952, "floor");
        Assert.Equal(PhysicsEvent.LostGround, PhysicsSystem.Step(body, [Floor], Dt, 1.0, Config)); // taskbar shrank: floor now at 1000
        Assert.Equal(BodyMode.Airborne, body.Mode);
    }

    [Fact]
    public void Grounded_hands_over_to_adjacent_platform_at_same_height()
    {
        var next = new Platform(PlatformKind.Floor, "next", 1000, 1920, 3840);
        var body = new PetBody();
        body.Land(1919, 1000, "floor");
        body.Feet = body.Feet with { X = 1921 };

        Assert.Equal(PhysicsEvent.None, PhysicsSystem.Step(body, [Floor, next], Dt, 1.0, Config));
        Assert.Equal(BodyMode.Grounded, body.Mode);
        Assert.Equal("next", body.GroundKey);
    }

    [Fact]
    public void Grounded_walks_off_edge_when_neighbour_is_lower()
    {
        var lower = new Platform(PlatformKind.Floor, "lower", 1048, 1920, 3840);
        var body = new PetBody();
        body.Land(1919, 1000, "floor");
        body.Feet = body.Feet with { X = 1921 };

        Assert.Equal(PhysicsEvent.LostGround, PhysicsSystem.Step(body, [Floor, lower], Dt, 1.0, Config));
    }

    [Fact]
    public void Grounded_falls_when_platform_disappears()
    {
        var body = new PetBody();
        body.Land(500, 1000, "floor");
        Assert.Equal(PhysicsEvent.LostGround, PhysicsSystem.Step(body, [], Dt, 1.0, Config));
        Assert.Null(body.GroundKey);
    }
}
