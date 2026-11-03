using DesktopPet.Core.Animation;
using DesktopPet.Core.Behavior;
using DesktopPet.Core.Math;
using DesktopPet.Core.Physics;
using DesktopPet.Core.Simulation;
using DesktopPet.Core.Tests.Fakes;
using DesktopPet.Core.World;
using static DesktopPet.Core.Tests.Fakes.SimulationExtensions;

namespace DesktopPet.Core.Tests.Behavior;

/// <summary>Climbing window sides and screen edges, sitting on title bars.</summary>
public class ClimbingTests
{
    private const long Win = 5;
    private static readonly PetConfig Config = PetConfig.Default;
    private static readonly double Size = Config.SizeDip;
    private static readonly double HalfWidth = Config.SizeDip * Config.BodyHalfWidthFraction;

    private static FakeDesktop Screen() => new FakeDesktop().Monitor(0, 0, 1920, 1080, taskbar: 40); // floor 1040

    /// <summary>Lands the pet at <paramref name="x"/> on whatever is below, then starts it walking.</summary>
    private static PetSimulation WalkFrom(DesktopSnapshot desktop, double x, bool facingLeft, Abstractions.IRandom random, double dropY = 100)
    {
        var sim = NewSim(desktop, random);
        sim.Place(new Vec2(x, dropY), Vec2.Zero);
        sim.RunUntil(desktop, Grounded);
        sim.ForceState(PetStateId.Walk, facingLeft);
        return sim;
    }

    // Walk.Enter rolls duration then "turn?" (0.99 = keep going); the next roll decides at the wall.
    private static ScriptedRandom ClimbAtWall(double then) => new(then, 0.99, 0.99, 0.0);

    // ---- Screen edges ----

    [Fact]
    public void Walking_into_the_screen_edge_may_start_climbing_it()
    {
        var desktop = Screen().Snapshot();
        var sim = WalkFrom(desktop, 1800, facingLeft: false, ClimbAtWall(then: 0.0));

        sim.RunUntil(desktop, s => s.Mode == BodyMode.Clinging, maxTicks: 200);

        Assert.Equal(PetStateId.Climb, sim.State);
        Assert.Equal(1920 - (Size / 2), sim.Current.FeetPos.X); // sprite flush against the edge
        Assert.False(sim.Current.FacingLeft);                   // facing the wall on its right
        Assert.Equal(Animations.Climb.Id, sim.Current.AnimationId);
    }

    [Fact]
    public void Otherwise_it_turns_at_the_screen_edge_as_before()
    {
        var desktop = Screen().Snapshot();
        var sim = WalkFrom(desktop, 1800, facingLeft: false, new FakeRandom(0.99));
        sim.RunUntil(desktop, s => s.Current.FacingLeft, maxTicks: 200);
        Assert.Equal(BodyMode.Grounded, sim.Mode);
    }

    [Fact]
    public void Climbing_the_screen_edge_turns_back_at_the_top_and_drops_off_at_the_bottom()
    {
        // One long, quick climb: up the whole edge, turn back at the top, all the way down, drop off.
        var desktop = Screen().Snapshot();
        var config = Config with { ClimbSeconds = new DurationRange(100, 100), ClimbSpeedDip = 300 };
        var sim = NewSim(desktop, ClimbAtWall(then: 0.0), config);
        sim.Place(new Vec2(100, 900), Vec2.Zero);
        sim.RunUntil(desktop, Grounded);
        sim.ForceState(PetStateId.Walk, facingLeft: true);
        sim.RunUntil(desktop, s => s.Mode == BodyMode.Clinging, maxTicks: 200);

        var highest = double.MaxValue;
        for (var i = 0; i < 60 * 20 && sim.Mode == BodyMode.Clinging; i++)
        {
            sim.Tick(Dt, desktop);
            if (sim.Mode != BodyMode.Clinging)
            {
                break; // let go at the bottom (pushing off the wall a little)
            }

            highest = System.Math.Min(highest, sim.Current.FeetPos.Y);
            Assert.Equal(Size / 2, sim.Current.FeetPos.X);
            Assert.True(sim.Current.FacingLeft); // the wall is on its left
        }

        Assert.InRange(highest - (Size / 2), 0, 6); // mid-body reached the top of the screen, no further
        sim.RunUntil(desktop, Grounded, maxTicks: 60 * 5);
        Assert.Equal(1040, sim.Current.FeetPos.Y);
    }

    // ---- Window sides ----

    [Fact]
    public void Climbs_up_a_window_side_and_over_onto_its_title_bar()
    {
        var desktop = Screen().Window(Win, 800, 600, 400, 440).Snapshot(); // reaches down to the floor
        var sim = WalkFrom(desktop, 700, facingLeft: false, ClimbAtWall(then: 0.0));

        sim.RunUntil(desktop, s => s.Mode == BodyMode.Clinging, maxTicks: 200);
        Assert.Equal(800 - (Size / 2), sim.Current.FeetPos.X);

        sim.RunUntil(desktop, Grounded, maxTicks: 60 * 20);

        Assert.Equal(PlatformBuilder.WindowKey(Win), sim.GroundKey);
        Assert.Equal(600, sim.Current.FeetPos.Y);
        Assert.InRange(sim.Current.FeetPos.X, 800 + HalfWidth, 800 + HalfWidth + 4);
    }

    [Fact]
    public void Walks_past_a_window_side_it_chose_not_to_climb_or_push()
    {
        var desktop = Screen().Window(Win, 800, 600, 400, 440).Snapshot();
        var sim = WalkFrom(desktop, 700, facingLeft: false, new FakeRandom(0.99));
        sim.RunUntil(desktop, s => s.Current.FeetPos.X > 900, maxTicks: 60 * 5);
        Assert.Equal(BodyMode.Grounded, sim.Mode);
        Assert.Empty(sim.TakeCommands());
    }

    [Fact]
    public void Climbs_down_the_side_at_the_end_of_a_window_top()
    {
        var desktop = Screen().Window(Win, 400, 500, 600, 540).Snapshot();
        // Walk.Enter: duration, keep heading; at the end: climb down (0.0); then climbing keeps choosing "down" (0.45).
        var sim = WalkFrom(desktop, 900, facingLeft: false, new ScriptedRandom(0.45, 0.99, 0.99, 0.0));
        Assert.Equal(500, sim.Current.FeetPos.Y);

        sim.RunUntil(desktop, s => s.Mode == BodyMode.Clinging, maxTicks: 200);
        Assert.Equal(1000 + (Size / 2), sim.Current.FeetPos.X); // right of the window's right side
        Assert.True(sim.Current.FacingLeft);                    // facing the window

        sim.RunUntil(desktop, Grounded, maxTicks: 60 * 30);
        Assert.Equal(1040, sim.Current.FeetPos.Y);
    }

    [Fact]
    public void Clinging_pet_rides_a_moved_window_and_falls_when_it_closes()
    {
        var desktop = Screen().Window(Win, 800, 300, 400, 740).Snapshot();
        var sim = NewSim(desktop, new FakeRandom(0.6)); // 0.6: holds on when choosing on the wall
        sim.Place(new Vec2(650, 600), new Vec2(900, 0)); // thrown into the window's left side
        sim.RunUntil(desktop, s => s.Mode == BodyMode.Clinging, maxTicks: 60);
        var y = sim.Current.FeetPos.Y;

        var moved = Screen().Window(Win, 900, 250, 400, 740).Snapshot();
        sim.Tick(Dt, moved);
        Assert.Equal(BodyMode.Clinging, sim.Mode);
        Assert.Equal(new Vec2(900 - (Size / 2), y - 50), sim.Current.FeetPos);

        sim.Tick(Dt, Screen().Snapshot());
        Assert.Equal(PetStateId.Fall, sim.State);
    }

    // ---- Getting onto walls from the air or the mouse ----

    [Fact]
    public void Thrown_into_the_screen_edge_grabs_on_when_slow_and_bounces_when_fast()
    {
        var desktop = Screen().Snapshot();

        var slow = NewSim(desktop, new FakeRandom(0.6));
        slow.Place(new Vec2(1800, 400), new Vec2(1200, -300));
        slow.RunUntil(desktop, s => s.Mode != BodyMode.Airborne, maxTicks: 60);
        Assert.Equal(BodyMode.Clinging, slow.Mode);
        Assert.Equal(PetStateId.Cling, slow.State);

        var fast = NewSim(desktop, new FakeRandom(0.6));
        fast.Place(new Vec2(1800, 400), new Vec2(Config.ClingMaxSpeedDip + 500, -300));
        fast.RunUntil(desktop, s => s.Velocity.X < 0, maxTicks: 60); // bounced
        Assert.Equal(BodyMode.Airborne, fast.Mode);
    }

    [Fact]
    public void Dropped_by_the_mouse_beside_a_window_grabs_its_side()
    {
        var desktop = Screen().Window(Win, 800, 300, 400, 740).Snapshot();
        var sim = NewSim(desktop, new FakeRandom(0.6));
        sim.Place(new Vec2(500, 900), Vec2.Zero);
        sim.RunUntil(desktop, Grounded);

        // Carry it so its right side is 6 px short of the window's left edge, hold still, let go.
        var target = new Vec2(800 - HalfWidth - 6, 600);
        var t = 10.0;
        sim.PointerDown(new Vec2(500, 1020), t);
        sim.PointerMove(new Vec2(510, 1020), t += Dt);
        for (var i = 0; i < 12; i++)
        {
            sim.PointerMove(target - new Vec2(0, 20), t += Dt);
            sim.Tick(Dt, desktop);
        }

        sim.PointerUp(target - new Vec2(0, 20), t);
        sim.Tick(Dt, desktop); // frames are built by ticks

        Assert.Equal(BodyMode.Clinging, sim.Mode);
        Assert.Equal(800 - (Size / 2), sim.Current.FeetPos.X, 3);
    }

    [Fact]
    public void Dropped_inside_a_window_does_not_grab_its_side_from_within()
    {
        var desktop = Screen().Window(Win, 800, 300, 400, 740).Snapshot();
        var sim = NewSim(desktop, new FakeRandom(0.6));
        sim.Place(new Vec2(810, 600), Vec2.Zero); // just inside the left edge
        sim.RunUntil(desktop, s => s.Mode != BodyMode.Airborne, maxTicks: 120);
        Assert.NotEqual(BodyMode.Clinging, sim.Mode);
    }

    // ---- Sitting on a title bar ----

    [Fact]
    public void Sitting_on_a_title_bar_perches_with_legs_over_the_edge()
    {
        var desktop = Screen().Window(Win, 400, 500, 600, 400).Snapshot();
        var sim = NewSim(desktop, new FakeRandom(0.99));
        sim.Place(new Vec2(700, 100), Vec2.Zero);
        sim.RunUntil(desktop, Grounded);
        sim.ForceState(PetStateId.Sit);
        sim.Tick(Dt, desktop);

        Assert.Equal(Animations.SitLedge.Id, sim.Current.AnimationId);
        Assert.Equal(Size * Animations.LedgeSinkFraction, sim.Current.SinkY, 6);
        Assert.Equal(500 + (Size * Animations.LedgeSinkFraction), sim.Current.Bounds.Bottom, 0);
    }

    [Fact]
    public void Sitting_on_the_floor_is_an_ordinary_sit()
    {
        var desktop = Screen().Snapshot();
        var sim = NewSim(desktop, new FakeRandom(0.99));
        sim.RunUntil(desktop, Grounded);
        sim.ForceState(PetStateId.Sit);
        sim.Tick(Dt, desktop);

        Assert.Equal(Animations.Sit.Id, sim.Current.AnimationId);
        Assert.Equal(0, sim.Current.SinkY);
    }

    // ---- Switched off ----

    [Fact]
    public void With_climbing_and_pushing_off_walls_only_turn_the_pet()
    {
        var desktop = Screen().Snapshot();
        var config = Config with { EnableClimbing = false, EnablePushing = false };
        var sim = NewSim(desktop, ClimbAtWall(then: 0.0), config);
        sim.Place(new Vec2(1800, 100), Vec2.Zero);
        sim.RunUntil(desktop, Grounded);
        sim.ForceState(PetStateId.Walk, facingLeft: false);

        sim.RunUntil(desktop, s => s.Current.FacingLeft, maxTicks: 200);
        Assert.Equal(BodyMode.Grounded, sim.Mode);
        Assert.Empty(sim.Walls);
    }
}
