using Deskling.Core.Animation;
using Deskling.Core.Behavior;
using Deskling.Core.Math;
using Deskling.Core.Physics;
using Deskling.Core.Simulation;
using Deskling.Core.Tests.Fakes;
using Deskling.Core.World;
using static Deskling.Core.Tests.Fakes.SimulationExtensions;

namespace Deskling.Core.Tests.Simulation;

/// <summary>Spawning, falling, landing and the safety net (M2).</summary>
public class PetSimulationTests
{
    [Fact]
    public void Spawns_at_top_centre_of_primary_and_lands_on_its_taskbar()
    {
        var desktop = new FakeDesktop()
            .Monitor(-1920, 0, 1920, 1080, primary: false)
            .Monitor(0, 0, 2560, 1440, scale: 1.5, taskbar: 48, primary: true)
            .Snapshot();
        var sim = NewSim(desktop);

        Assert.Equal(BodyMode.Airborne, sim.Mode);
        Assert.Equal(PetStateId.Fall, sim.State);
        Assert.Equal(new Vec2(1280, 128), sim.Current.FeetPos); // top of work area + 64 DIP × 1.5 = 96 px, snapped up to 2× the art
        Assert.Equal(Animations.Fall.Id, sim.Current.AnimationId);

        sim.RunUntil(desktop, Grounded);

        Assert.Equal(new Vec2(1280, 1392), sim.Current.FeetPos);
        Assert.Equal(1392, sim.Current.Bounds.Bottom);
    }

    [Fact]
    public void Fall_time_matches_gravity()
    {
        // 1080 - 64 = 1016 px to fall at 3000 px/s² → ~0.82 s → ~49 ticks (terminal velocity not reached).
        var desktop = new FakeDesktop().Monitor(0, 0, 1920, 1080).Snapshot();
        var ticks = NewSim(desktop).RunUntil(desktop, Grounded);
        Assert.InRange(ticks, 45, 52);
    }

    [Fact]
    public void Lands_on_the_floor_of_the_monitor_it_is_over()
    {
        var desktop = new FakeDesktop()
            .Monitor(0, 0, 1920, 1080, taskbar: 40)
            .Monitor(1920, -200, 2560, 1440, scale: 1.5)
            .Snapshot();
        var sim = NewSim(desktop);
        sim.Place(new Vec2(3000, -100), Vec2.Zero);

        sim.RunUntil(desktop, Grounded);

        Assert.Equal(1240, sim.Current.FeetPos.Y);
        Assert.Equal(128, sim.Current.Size.X); // picked up monitor B's scale
    }

    [Fact]
    public void Plays_land_then_picks_a_behavior()
    {
        var desktop = new FakeDesktop().Monitor(0, 0, 1920, 1080).Snapshot();
        var sim = NewSim(desktop);

        sim.RunUntil(desktop, Grounded);
        Assert.Equal(PetStateId.Land, sim.State);
        Assert.Equal(Animations.Land.Id, sim.Current.AnimationId);

        sim.RunUntil(desktop, s => s.State == PetStateId.Idle, maxTicks: 60);
        Assert.Equal(Animations.Idle.Id, sim.Current.AnimationId);
    }

    [Fact]
    public void Follows_taskbar_growth_and_falls_when_it_shrinks()
    {
        var small = new FakeDesktop().Monitor(0, 0, 1920, 1080, taskbar: 48).Snapshot();
        var large = new FakeDesktop().Monitor(0, 0, 1920, 1080, taskbar: 96).Snapshot();
        var sim = NewSim(small);
        sim.RunUntil(small, Grounded);

        sim.Tick(Dt, large);
        Assert.Equal(984, sim.Current.FeetPos.Y);
        Assert.Equal(BodyMode.Grounded, sim.Mode);

        sim.Tick(Dt, small);
        Assert.Equal(PetStateId.Fall, sim.State);
        sim.RunUntil(small, Grounded, maxTicks: 30);
        Assert.Equal(1032, sim.Current.FeetPos.Y);
    }

    [Fact]
    public void Monitor_unplugged_under_pet_falls_then_respawns_on_remaining_monitor()
    {
        var two = new FakeDesktop().Monitor(0, 0, 1920, 1080).Monitor(1920, 0, 1920, 1080).Snapshot();
        var one = new FakeDesktop().Monitor(0, 0, 1920, 1080).Snapshot();
        var sim = NewSim(two);
        sim.Place(new Vec2(3000, 500), Vec2.Zero);
        sim.RunUntil(two, Grounded);

        sim.RunUntil(one, s => s.RespawnCount == 1, maxTicks: 60);
        Assert.Equal(new Vec2(960, 64), sim.Current.FeetPos);
        Assert.Equal(sim.Current, sim.Previous); // no interpolation streak
        Assert.Equal(PetStateId.Fall, sim.State);

        sim.RunUntil(one, Grounded);
        Assert.Equal(1080, sim.Current.FeetPos.Y);
    }

    [Fact]
    public void Brief_excursion_outside_is_tolerated()
    {
        var desktop = new FakeDesktop().Monitor(0, 0, 1920, 1080).Snapshot();
        var sim = NewSim(desktop);
        sim.Place(new Vec2(-10, 500), Vec2.Zero);
        sim.Run(desktop, 10); // well under the 0.5 s grace
        Assert.Equal(0, sim.RespawnCount);
    }

    [Fact]
    public void Falling_into_gap_below_shorter_monitor_respawns()
    {
        // A is shorter than B; dropping at A's x below A's bottom is unrecoverable.
        var desktop = new FakeDesktop().Monitor(0, 0, 1920, 1080).Monitor(1920, -200, 2560, 1440).Snapshot();
        var sim = NewSim(desktop);
        sim.Place(new Vec2(500, 1200), Vec2.Zero);

        sim.RunUntil(desktop, s => s.RespawnCount == 1, maxTicks: 60);
        sim.RunUntil(desktop, Grounded);
        Assert.Equal(1080, sim.Current.FeetPos.Y);
    }

    [Fact]
    public void No_monitors_freezes_physics()
    {
        var desktop = new FakeDesktop().Monitor(0, 0, 1920, 1080).Snapshot();
        var sim = NewSim(desktop);
        var before = sim.Current.FeetPos;
        sim.Run(DesktopSnapshot.Empty, 120);
        Assert.Equal(before, sim.Current.FeetPos);
        Assert.Equal(0, sim.RespawnCount);
    }

    [Fact]
    public void Survives_construction_without_monitors()
    {
        var sim = NewSim(DesktopSnapshot.Empty);
        sim.Run(DesktopSnapshot.Empty, 10);
    }

    [Fact]
    public void Sprite_rect_sits_on_feet_point()
    {
        var r = PetFrame.SpriteRect(new Vec2(1280, 1392), new Vec2(96, 96));
        Assert.Equal(new RectI(1232, 1296, 1328, 1392), r);
    }
}
