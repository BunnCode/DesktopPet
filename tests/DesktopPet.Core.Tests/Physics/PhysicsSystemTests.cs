using DesktopPet.Core.Math;
using DesktopPet.Core.Physics;
using DesktopPet.Core.Simulation;
using DesktopPet.Core.Tests.Fakes;
using DesktopPet.Core.World;

namespace DesktopPet.Core.Tests.Physics;

public class PhysicsSystemTests
{
    private const double Dt = 1.0 / 60.0;
    private static readonly PetConfig Config = PetConfig.Default;
    private static readonly Platform Floor = new(PlatformKind.Floor, "floor", Y: 1000, Left: 0, Right: 1920);

    /// <summary>A huge monitor so walls never interfere unless a test wants them.</summary>
    private static readonly IReadOnlyList<MonitorInfo> Open = new FakeDesktop().Monitor(-50_000, -50_000, 100_000, 100_000).Snapshot().Monitors;

    /// <summary>One ordinary 1920×1000 screen whose bottom is the <see cref="Floor"/>.</summary>
    private static readonly IReadOnlyList<MonitorInfo> Screen = new FakeDesktop().Monitor(0, 0, 1920, 1000).Snapshot().Monitors;

    private static PhysicsWorld World(params Platform[] platforms) => new(platforms, Open, 1.0);

    private static PhysicsWorld ScreenWorld(double scale = 1.0) => new([Floor], Screen, scale);

    private static PetBody Airborne(double x, double y, double vx = 0, double vy = 0)
    {
        var body = new PetBody { Feet = new Vec2(x, y) };
        body.Release(new Vec2(vx, vy));
        return body;
    }

    private static PetBody GroundedAt(double x, double y = 1000, double skid = 0)
    {
        var body = new PetBody();
        body.Land(x, Floor with { Y = y }, skid);
        return body;
    }

    // ---- Gravity and landing (M2) ----

    [Fact]
    public void Gravity_accelerates_downward()
    {
        var body = Airborne(100, 0);
        PhysicsSystem.Step(body, World(), Dt, Config);
        Assert.Equal(Config.GravityDip * Dt, body.Velocity.Y, 6);
        Assert.True(body.Feet.Y > 0);
    }

    [Fact]
    public void Gravity_and_terminal_velocity_scale_with_dpi()
    {
        var body = Airborne(100, 0, vy: 1e6);
        PhysicsSystem.Step(body, World() with { Scale = 1.5 }, Dt, Config);
        Assert.Equal(Config.TerminalVelocityDip * 1.5, body.Velocity.Y, 6);
    }

    [Fact]
    public void Never_exceeds_terminal_velocity()
    {
        var body = Airborne(100, -40_000);
        for (var i = 0; i < 1000; i++)
        {
            PhysicsSystem.Step(body, World(), Dt, Config);
            Assert.True(body.Velocity.Y <= Config.TerminalVelocityDip + 1e-9);
        }

        Assert.Equal(Config.TerminalVelocityDip, body.Velocity.Y, 6);
    }

    [Fact]
    public void Lands_exactly_on_platform_and_stops()
    {
        var body = Airborne(500, 995, vy: 600);
        var result = PhysicsSystem.Step(body, World(Floor), Dt, Config);

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
        var result = PhysicsSystem.Step(body, World(Floor), Dt, Config with { TerminalVelocityDip = 1e9 });
        Assert.Equal(PhysicsEvent.Landed, result);
        Assert.Equal(1000, body.Feet.Y);
    }

    [Fact]
    public void Lands_on_highest_platform_crossed()
    {
        var upper = new Platform(PlatformKind.Floor, "upper", 600, 0, 1000);
        var body = Airborne(500, 500, vy: 1e6);
        PhysicsSystem.Step(body, World(Floor, upper), Dt, Config with { TerminalVelocityDip = 1e9 });
        Assert.Equal("upper", body.GroundKey);
    }

    [Fact]
    public void Diagonal_landing_uses_x_at_crossing_point()
    {
        // From (0, 900) to roughly (200, 1100): crosses y=1000 halfway, at x≈100. Platform only spans [50, 150).
        var narrow = new Platform(PlatformKind.Floor, "narrow", 1000, 50, 150);
        var body = Airborne(0, 900, vx: 12000, vy: 12000);
        PhysicsSystem.Step(body, World(narrow), Dt, Config with { TerminalVelocityDip = 1e9, GravityDip = 0, AirDragPerSecond = 0 });
        Assert.Equal(BodyMode.Grounded, body.Mode);
        Assert.Equal(100, body.Feet.X, 1);
    }

    [Fact]
    public void Platforms_are_one_way_moving_up_passes_through()
    {
        var body = Airborne(500, 1010, vy: -6000);
        var result = PhysicsSystem.Step(body, World(Floor), Dt, Config);
        Assert.Equal(PhysicsEvent.None, result);
        Assert.True(body.Feet.Y < 1000);
        Assert.Equal(BodyMode.Airborne, body.Mode);
    }

    [Fact]
    public void Misses_platform_when_beside_it()
    {
        var body = Airborne(2000, 995, vy: 600);
        Assert.Equal(PhysicsEvent.None, PhysicsSystem.Step(body, World(Floor), Dt, Config));
        Assert.Equal(BodyMode.Airborne, body.Mode);
    }

    // ---- Ground tracking (M2/M3) ----

    [Fact]
    public void Grounded_follows_platform_that_moves_up()
    {
        var body = GroundedAt(500);
        var raised = Floor with { Y = 952 }; // taskbar grew
        Assert.Equal(PhysicsEvent.None, PhysicsSystem.Step(body, World(raised), Dt, Config));
        Assert.Equal(952, body.Feet.Y);
    }

    [Fact]
    public void Grounded_falls_when_platform_drops_away()
    {
        var body = GroundedAt(500, 952);
        Assert.Equal(PhysicsEvent.LostGround, PhysicsSystem.Step(body, World(Floor), Dt, Config)); // floor now at 1000
        Assert.Equal(BodyMode.Airborne, body.Mode);
    }

    [Fact]
    public void Grounded_hands_over_to_adjacent_platform_at_same_height()
    {
        var next = new Platform(PlatformKind.Floor, "next", 1000, 1920, 3840);
        var body = GroundedAt(1919);
        body.Feet = body.Feet with { X = 1921 };

        Assert.Equal(PhysicsEvent.None, PhysicsSystem.Step(body, World(Floor, next), Dt, Config));
        Assert.Equal(BodyMode.Grounded, body.Mode);
        Assert.Equal("next", body.GroundKey);
    }

    [Fact]
    public void Grounded_walks_off_edge_when_neighbour_is_lower()
    {
        var lower = new Platform(PlatformKind.Floor, "lower", 1048, 1920, 3840);
        var body = GroundedAt(1919);
        body.Feet = body.Feet with { X = 1921 };

        Assert.Equal(PhysicsEvent.LostGround, PhysicsSystem.Step(body, World(Floor, lower), Dt, Config));
    }

    [Fact]
    public void Grounded_falls_when_platform_disappears()
    {
        var body = GroundedAt(500);
        Assert.Equal(PhysicsEvent.LostGround, PhysicsSystem.Step(body, World(), Dt, Config));
        Assert.Null(body.GroundKey);
    }

    // ---- Throws: drag, walls, skids (M4) ----

    [Fact]
    public void Held_body_ignores_gravity()
    {
        var body = Airborne(500, 500);
        body.Grab();
        for (var i = 0; i < 60; i++)
        {
            Assert.Equal(PhysicsEvent.None, PhysicsSystem.Step(body, World(Floor), Dt, Config));
        }

        Assert.Equal(new Vec2(500, 500), body.Feet);
        Assert.Equal(BodyMode.Held, body.Mode);
    }

    [Fact]
    public void Air_drag_slows_horizontal_motion_only()
    {
        var body = Airborne(0, 0, vx: 1000);
        PhysicsSystem.Step(body, World(), 1.0, Config with { GravityDip = 0 });
        Assert.Equal(1000 * System.Math.Exp(-Config.AirDragPerSecond), body.Velocity.X, 6);
        Assert.Equal(0, body.Velocity.Y);
    }

    [Fact]
    public void Bounces_off_screen_edge_losing_energy()
    {
        var halfWidth = Config.SizeDip * Config.BodyHalfWidthFraction;
        var body = Airborne(1920 - halfWidth - 5, 500, vx: 1200);

        var result = PhysicsSystem.Step(body, ScreenWorld(), Dt, Config with { AirDragPerSecond = 0 });

        Assert.Equal(PhysicsEvent.HitWall, result);
        Assert.Equal(-1200 * Config.WallRestitution, body.Velocity.X, 6);
        Assert.Equal(1920 - halfWidth - 5, body.Feet.X); // didn't penetrate
    }

    [Fact]
    public void Thrown_up_above_the_screen_is_not_a_wall()
    {
        var body = Airborne(960, -300, vx: 500);
        var result = PhysicsSystem.Step(body, ScreenWorld(), Dt, Config);
        Assert.Equal(PhysicsEvent.None, result);
        Assert.True(body.Velocity.X > 0);
    }

    [Fact]
    public void Landing_keeps_horizontal_speed_then_skids_to_a_stop()
    {
        var body = Airborne(500, 995, vx: 600, vy: 600);
        Assert.Equal(PhysicsEvent.Landed, PhysicsSystem.Step(body, ScreenWorld(), Dt, Config with { AirDragPerSecond = 0 }));
        Assert.Equal(600, body.Velocity.X, 6);

        var x0 = body.Feet.X;
        var ticks = 0;
        while (body.Velocity.X > 0 && ticks++ < 600)
        {
            PhysicsSystem.Step(body, ScreenWorld(), Dt, Config);
            Assert.Equal(1000, body.Feet.Y);
        }

        // v² / 2a = 600² / (2 × 2400) = 75 px, reached in 0.25 s.
        Assert.Equal(75, body.Feet.X - x0, 0);
        Assert.InRange(ticks, 14, 16);
        Assert.Equal(BodyMode.Grounded, body.Mode);
    }

    [Fact]
    public void Skid_stops_at_wall()
    {
        var halfWidth = Config.SizeDip * Config.BodyHalfWidthFraction;
        var body = GroundedAt(1920 - halfWidth - 2, skid: 2000);

        Assert.Equal(PhysicsEvent.HitWall, PhysicsSystem.Step(body, ScreenWorld(), Dt, Config));
        Assert.Equal(Vec2.Zero, body.Velocity);
        Assert.True(body.Feet.X <= 1920 - halfWidth);
    }

    [Fact]
    public void Skidding_off_a_platform_edge_keeps_momentum()
    {
        var shelf = new Platform(PlatformKind.Floor, "floor", 1000, 0, 600);
        var body = GroundedAt(595, skid: 900);

        Assert.Equal(PhysicsEvent.LostGround, PhysicsSystem.Step(body, World(shelf), Dt, Config));
        Assert.True(body.Velocity.X > 800);
    }
}
