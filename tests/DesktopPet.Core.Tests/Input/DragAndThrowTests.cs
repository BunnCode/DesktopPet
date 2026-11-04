using DesktopPet.Core.Behavior;
using DesktopPet.Core.Math;
using DesktopPet.Core.Physics;
using DesktopPet.Core.Simulation;
using DesktopPet.Core.Tests.Fakes;
using DesktopPet.Core.World;
using static DesktopPet.Core.Tests.Fakes.SimulationExtensions;

namespace DesktopPet.Core.Tests.Input;

public class DragAndThrowTests
{
    private static readonly DesktopSnapshot Desktop = new FakeDesktop().Monitor(0, 0, 1920, 1080).Snapshot();
    private static readonly PetConfig Config = PetConfig.Default;

    /// <summary>A pet standing at (960, 1080), idle for a long time (random fixed at 0.99 → long durations).</summary>
    private static PetSimulation StandingPet(DesktopSnapshot? desktop = null, double x = 960)
    {
        desktop ??= Desktop;
        var sim = NewSim(desktop, new FakeRandom(0.99));
        sim.Place(new Vec2(x, 900), Vec2.Zero);
        sim.RunUntil(desktop, Grounded);
        sim.ForceState(PetStateId.Idle);
        return sim;
    }

    /// <summary>
    /// Presses on the pet's body, drags along <paramref name="path"/> one point per tick (as the host does while held),
    /// and returns the time of the last sample.
    /// </summary>
    private static double Drag(PetSimulation sim, DesktopSnapshot desktop, Vec2 press, IEnumerable<Vec2> path, double t0 = 10)
    {
        var t = t0;
        sim.PointerDown(press, t);
        foreach (var p in path)
        {
            t += Dt;
            sim.PointerMove(p, t);
            sim.Tick(Dt, desktop);
        }

        return t;
    }

    private static IEnumerable<Vec2> Line(Vec2 from, Vec2 velocityPerSecond, int ticks) =>
        Enumerable.Range(1, ticks).Select(i => from + (velocityPerSecond * (i * Dt)));

    [Fact]
    public void Click_without_moving_pets_the_pet()
    {
        var sim = StandingPet();
        sim.PointerDown(new Vec2(960, 1050), 10);
        sim.PointerUp(new Vec2(960, 1050), 10.1);

        Assert.Equal(PetStateId.Happy, sim.State);
        Assert.Equal(BodyMode.Grounded, sim.Mode);
    }

    [Fact]
    public void Movement_below_threshold_is_still_a_click()
    {
        var sim = StandingPet();
        sim.PointerDown(new Vec2(960, 1050), 10);
        sim.PointerMove(new Vec2(962, 1048), 10.05); // ~2.8 px < 4 DIP
        Assert.False(sim.IsHeld);
        sim.PointerUp(new Vec2(962, 1048), 10.1);

        Assert.Equal(PetStateId.Happy, sim.State);
    }

    [Fact]
    public void Happy_reaction_ends_and_life_goes_on()
    {
        var sim = StandingPet();
        sim.PointerDown(new Vec2(960, 1050), 10);
        sim.PointerUp(new Vec2(960, 1050), 10.1);
        sim.RunSeconds(Desktop, Config.HappySeconds.Max + 0.1);
        Assert.NotEqual(PetStateId.Happy, sim.State);
    }

    [Fact]
    public void Drag_picks_up_and_carries_the_pet_without_jumping()
    {
        var sim = StandingPet();
        var press = new Vec2(950, 1040); // grabbed 10 px left of and 40 px above the feet

        Drag(sim, Desktop, press, [new Vec2(960, 1040), new Vec2(1100, 700)]);

        Assert.True(sim.IsHeld);
        Assert.Equal(PetStateId.Held, sim.State);
        Assert.Equal(new Vec2(1110, 740), sim.Current.FeetPos); // same offset from the cursor as at the press
    }

    [Fact]
    public void Held_pet_does_not_fall_or_respawn()
    {
        var sim = StandingPet();
        Drag(sim, Desktop, new Vec2(960, 1050), [new Vec2(970, 1050), new Vec2(500, 300)]);
        var held = sim.Current.FeetPos;

        sim.RunSeconds(Desktop, 2);

        Assert.Equal(held, sim.Current.FeetPos);
        Assert.Equal(0, sim.RespawnCount);
    }

    [Fact]
    public void Release_while_still_drops_straight_down()
    {
        var sim = StandingPet();
        var t = Drag(sim, Desktop, new Vec2(960, 1050), [new Vec2(970, 1050), .. Enumerable.Repeat(new Vec2(500, 300), 30)]);
        sim.PointerUp(new Vec2(500, 300), t);

        Assert.Equal(PetStateId.Fall, sim.State);
        Assert.Equal(Vec2.Zero, sim.Velocity);
        sim.RunUntil(Desktop, Grounded);
        Assert.Equal(500 + 0, sim.Current.FeetPos.X, 0);
    }

    [Fact]
    public void Throw_carries_the_drag_velocity_and_faces_that_way()
    {
        var sim = StandingPet();
        var start = new Vec2(960, 700);
        var t = Drag(sim, Desktop, new Vec2(960, 1050), [new Vec2(960, 1040), .. Line(start, new Vec2(-800, -200), 12)]);
        var last = start + (new Vec2(-800, -200) * (12 * Dt));
        sim.PointerUp(last, t);

        Assert.Equal(BodyMode.Airborne, sim.Mode);
        Assert.Equal(-800, sim.Velocity.X, 0);
        Assert.Equal(-200, sim.Velocity.Y, 0);
        sim.Tick(Dt, Desktop);
        Assert.True(sim.Current.FacingLeft);
    }

    [Fact]
    public void Throw_speed_is_capped()
    {
        var sim = StandingPet();
        var t = Drag(sim, Desktop, new Vec2(960, 1050), Line(new Vec2(960, 1040), new Vec2(50_000, 0), 6));
        sim.PointerUp(new Vec2(960 + (50_000 * 6 * Dt), 1040), t);

        Assert.Equal(Config.MaxThrowSpeedDip, sim.Velocity.Length, 3);
    }

    [Fact]
    public void Thrown_pet_bounces_off_the_wall_lands_skids_and_settles()
    {
        var sim = StandingPet();
        var t = Drag(sim, Desktop, new Vec2(960, 1050), [new Vec2(960, 1040), .. Line(new Vec2(1500, 600), new Vec2(2500, -300), 8)]);
        sim.PointerUp(new Vec2(1500, 600) + (new Vec2(2500, -300) * (8 * Dt)), t);

        var halfWidth = Config.SizeDip * Config.BodyHalfWidthFraction;
        var bounced = false;
        sim.RunUntil(Desktop, s =>
        {
            Assert.True(s.Current.FeetPos.X <= 1920 - halfWidth + 1e-6, "went through the wall");
            bounced |= s.Velocity.X < 0;
            return s.Mode == BodyMode.Grounded;
        });

        Assert.True(bounced);
        Assert.Equal(PetStateId.Land, sim.State);
        Assert.True(sim.Velocity.X < 0); // still sliding back left

        sim.RunUntil(Desktop, s => s.State != PetStateId.Land, maxTicks: 120);
        Assert.Equal(0, sim.Velocity.X);
        Assert.Equal(0, sim.RespawnCount);
    }

    [Fact]
    public void Thrown_high_comes_back_down_without_respawning()
    {
        var sim = StandingPet();
        var t = Drag(sim, Desktop, new Vec2(960, 1050), [new Vec2(960, 1040), .. Line(new Vec2(960, 200), new Vec2(0, -3000), 6)]);
        sim.PointerUp(new Vec2(960, 200 - (3000 * 6 * Dt)), t);

        var highest = double.MaxValue;
        sim.RunUntil(Desktop, s =>
        {
            highest = System.Math.Min(highest, s.Current.FeetPos.Y);
            return s.Mode == BodyMode.Grounded;
        });

        Assert.True(highest < 0); // it really left the screen upward
        Assert.Equal(0, sim.RespawnCount);
        Assert.Equal(1080, sim.Current.FeetPos.Y);
    }

    [Fact]
    public void Can_catch_the_pet_mid_air()
    {
        var sim = NewSim(Desktop, new FakeRandom(0.99));
        sim.Run(Desktop, 10); // falling from the top
        Assert.Equal(BodyMode.Airborne, sim.Mode);

        var feet = sim.Current.FeetPos;
        Drag(sim, Desktop, feet - new Vec2(0, 20), [feet - new Vec2(0, 10)]);

        Assert.True(sim.IsHeld);
        sim.RunSeconds(Desktop, 1);
        Assert.True(sim.IsHeld);
    }

    [Fact]
    public void A_click_is_reported_once_with_what_it_did()
    {
        var sim = StandingPet();
        sim.PointerDown(new Vec2(960, 1050), 10);
        sim.PointerUp(new Vec2(960, 1050), 10.1);

        Assert.Equal(PetClick.Petted, sim.TakeClick());
        Assert.Null(sim.TakeClick());
    }

    [Fact]
    public void Clicking_a_sleeping_pet_reports_waking_it()
    {
        var sim = StandingPet();
        sim.ForceState(PetStateId.Sleep);
        sim.PointerDown(new Vec2(960, 1050), 10);
        sim.PointerUp(new Vec2(960, 1050), 10.1);

        Assert.Equal(PetClick.WokenUp, sim.TakeClick());
    }

    [Fact]
    public void Clicking_a_falling_pet_is_reported_as_a_poke()
    {
        var sim = NewSim(Desktop, new FakeRandom(0.99));
        sim.Run(Desktop, 10);
        sim.PointerDown(sim.Current.FeetPos, 1);
        sim.PointerUp(sim.Current.FeetPos, 1.05);

        Assert.Equal(PetClick.Poked, sim.TakeClick());
    }

    [Fact]
    public void Drags_and_throws_are_not_clicks()
    {
        var sim = StandingPet();
        var t = Drag(sim, Desktop, new Vec2(960, 1050), Line(new Vec2(960, 1050), new Vec2(0, -800), 10));
        sim.PointerUp(new Vec2(960, 1050 - (800 * 10 * Dt)), t);
        Assert.Null(sim.TakeClick());

        var dropped = StandingPet();
        Drag(dropped, Desktop, new Vec2(960, 1050), [new Vec2(970, 1050), new Vec2(700, 500)]);
        dropped.PointerCancel();
        Assert.Null(dropped.TakeClick());
    }

    [Fact]
    public void Clicking_a_falling_pet_does_nothing()
    {
        var sim = NewSim(Desktop, new FakeRandom(0.99));
        sim.Run(Desktop, 10);
        sim.PointerDown(sim.Current.FeetPos, 1);
        sim.PointerUp(sim.Current.FeetPos, 1.05);
        Assert.Equal(PetStateId.Fall, sim.State);
    }

    [Fact]
    public void Cancel_drops_the_pet_in_place()
    {
        var sim = StandingPet();
        Drag(sim, Desktop, new Vec2(960, 1050), [new Vec2(970, 1050), new Vec2(700, 500)]);
        sim.PointerCancel();

        Assert.Equal(PetStateId.Fall, sim.State);
        Assert.Equal(Vec2.Zero, sim.Velocity);
        sim.PointerUp(new Vec2(0, 0), 99); // late button-up after cancel is ignored
        Assert.Equal(PetStateId.Fall, sim.State);
    }

    [Fact]
    public void Dragging_to_another_monitor_adopts_its_scale()
    {
        var desktop = new FakeDesktop().Monitor(0, 0, 1920, 1080).Monitor(1920, 0, 2560, 1440, scale: 1.5).Snapshot();
        var sim = StandingPet(desktop);
        Drag(sim, desktop, new Vec2(960, 1050), [new Vec2(970, 1050), new Vec2(3000, 700)]);
        sim.Tick(Dt, desktop);

        Assert.Equal(96, sim.Current.Size.X);
    }
}
