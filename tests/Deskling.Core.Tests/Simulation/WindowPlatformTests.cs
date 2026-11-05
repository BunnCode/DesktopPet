using Deskling.Core.Behavior;
using Deskling.Core.Math;
using Deskling.Core.Physics;
using Deskling.Core.Simulation;
using Deskling.Core.Tests.Fakes;
using Deskling.Core.World;
using static Deskling.Core.Tests.Fakes.SimulationExtensions;

namespace Deskling.Core.Tests.Simulation;

/// <summary>Windows as platforms (M5): landing, walking, riding, falling when they go away.</summary>
public class WindowPlatformTests
{
    private const long Win = 42;

    private static FakeDesktop Screen() => new FakeDesktop().Monitor(0, 0, 1920, 1080, taskbar: 40); // floor 1040

    private static DesktopSnapshot WithWindow(int x, int y, int w = 800, int h = 500) => Screen().Window(Win, x, y, w, h).Snapshot();

    /// <summary>Drops the pet above (x, y) and lets it land; behavior fixed to long idles.</summary>
    private static PetSimulation DroppedOn(DesktopSnapshot desktop, double x, double fromY = 100)
    {
        var sim = NewSim(desktop, new FakeRandom(0.0));
        sim.Place(new Vec2(x, fromY), Vec2.Zero);
        sim.RunUntil(desktop, Grounded);
        return sim;
    }

    [Fact]
    public void Falling_pet_lands_on_window_top()
    {
        var desktop = WithWindow(400, 500);
        var sim = DroppedOn(desktop, 800);

        Assert.Equal(new Vec2(800, 500), sim.Current.FeetPos);
        Assert.Equal(PlatformBuilder.WindowKey(Win), sim.GroundKey);
    }

    [Fact]
    public void Misses_window_when_falling_beside_it()
    {
        var sim = DroppedOn(WithWindow(400, 500), 1500);
        Assert.Equal(1040, sim.Current.FeetPos.Y);
    }

    [Fact]
    public void Window_closes_and_pet_falls_to_the_floor()
    {
        var desktop = WithWindow(400, 500);
        var sim = DroppedOn(desktop, 800);

        var closed = Screen().Snapshot();
        sim.Tick(Dt, closed);
        Assert.Equal(PetStateId.Fall, sim.State);

        sim.RunUntil(closed, Grounded);
        Assert.Equal(new Vec2(800, 1040), sim.Current.FeetPos);
    }

    [Fact]
    public void Window_maximized_under_pet_is_no_longer_ground()
    {
        var sim = DroppedOn(WithWindow(400, 500), 800);
        var maximized = Screen().Window(Win, 0, 0, 1920, 1040, maximized: true).Snapshot();

        sim.RunUntil(maximized, Grounded);
        Assert.Equal(1040, sim.Current.FeetPos.Y);
    }

    [Fact]
    public void Rides_a_window_dragged_around()
    {
        var sim = DroppedOn(WithWindow(400, 500), 800);

        // Drag the window right and up over several snapshots; the pet keeps its spot on the title bar.
        for (var step = 1; step <= 10; step++)
        {
            sim.Tick(Dt, WithWindow(400 + (step * 30), 500 - (step * 20)));
            Assert.Equal(BodyMode.Grounded, sim.Mode);
        }

        Assert.Equal(new Vec2(1100, 300), sim.Current.FeetPos);
        Assert.NotEqual(PetStateId.Fall, sim.State); // never lost its footing (may still be finishing its landing squash)
    }

    [Fact]
    public void Resizing_a_window_from_the_left_does_not_drag_the_pet()
    {
        var sim = DroppedOn(WithWindow(400, 500), 800);
        sim.Tick(Dt, Screen().Window(Win, 300, 500, 900, 500).Snapshot()); // left edge pulled out by 100
        Assert.Equal(800, sim.Current.FeetPos.X);
        Assert.Equal(BodyMode.Grounded, sim.Mode);
    }

    [Fact]
    public void Rides_a_window_dragged_down_instead_of_falling()
    {
        var sim = DroppedOn(WithWindow(400, 300), 800);
        sim.Tick(Dt, WithWindow(400, 450));
        Assert.Equal(BodyMode.Grounded, sim.Mode);
        Assert.Equal(450, sim.Current.FeetPos.Y);
    }

    [Fact]
    public void Falls_off_when_the_window_slides_out_from_under()
    {
        var sim = DroppedOn(WithWindow(400, 500), 450); // near the left end
        sim.Tick(Dt, Screen().Window(Win, 400, 500, 800, 500).Window(99, 0, 0, 10, 10).Snapshot()); // unrelated change
        Assert.Equal(BodyMode.Grounded, sim.Mode);

        // The window gets narrower from the left (resized): the pet's spot is no longer on it.
        sim.Tick(Dt, Screen().Window(Win, 600, 500, 600, 500).Snapshot());
        Assert.Equal(PetStateId.Fall, sim.State);
    }

    [Fact]
    public void Another_window_brought_to_front_over_the_pets_spot_makes_it_fall()
    {
        var sim = DroppedOn(WithWindow(400, 500), 800);

        var covered = Screen()
            .Window(7, 600, 300, 600, 600) // now in front, covering y=500 for x in 600..1200
            .Window(Win, 400, 500, 800, 500)
            .Snapshot();
        sim.Tick(Dt, covered);
        Assert.Equal(PetStateId.Fall, sim.State);

        sim.RunUntil(covered, Grounded);
        Assert.Equal(1040, sim.Current.FeetPos.Y); // fell behind/through to the floor
    }

    [Fact]
    public void Walks_along_a_window_and_off_its_end()
    {
        var desktop = WithWindow(400, 500);
        var sim = NewSim(desktop, new FakeRandom(0.99)); // long walks, no random turns
        sim.Place(new Vec2(1100, 100), Vec2.Zero);
        sim.RunUntil(desktop, Grounded);
        sim.ForceState(PetStateId.Walk, facingLeft: false);

        sim.RunUntil(desktop, s => s.State == PetStateId.Fall, maxTicks: 200);
        Assert.True(sim.Current.FeetPos.X >= 1200);

        sim.RunUntil(desktop, Grounded);
        Assert.Equal(1040, sim.Current.FeetPos.Y);
    }

    [Fact]
    public void Walking_over_a_gap_between_visible_segments_falls_into_it()
    {
        var desktop = Screen()
            .Window(1, 700, 300, 100, 700)   // front, covers the middle of window 2's top
            .Window(2, 400, 500, 800, 500)
            .Snapshot();
        var sim = NewSim(desktop, new FakeRandom(0.99));
        sim.Place(new Vec2(650, 100), Vec2.Zero);
        sim.RunUntil(desktop, Grounded);
        Assert.Equal(500, sim.Current.FeetPos.Y);

        sim.ForceState(PetStateId.Walk, facingLeft: false);
        sim.RunUntil(desktop, s => s.State == PetStateId.Fall, maxTicks: 200);
        Assert.InRange(sim.Current.FeetPos.X, 700, 702);
    }

    [Fact]
    public void Thrown_pet_lands_on_a_window()
    {
        var desktop = WithWindow(400, 500);
        var sim = NewSim(desktop, new FakeRandom(0.0));
        sim.Place(new Vec2(200, 300), new Vec2(900, -500));

        sim.RunUntil(desktop, Grounded);

        Assert.Equal(500, sim.Current.FeetPos.Y);
        Assert.InRange(sim.Current.FeetPos.X, 400, 1200);
    }

    [Fact]
    public void Thrown_up_from_below_passes_through_the_window_top()
    {
        var desktop = WithWindow(400, 500);
        var sim = NewSim(desktop, new FakeRandom(0.0));
        sim.Place(new Vec2(800, 700), new Vec2(0, -2000)); // inside the window area, moving up through y=500

        var crossedAbove = false;
        sim.RunUntil(desktop, s =>
        {
            crossedAbove |= s.Current.FeetPos.Y < 500;
            return s.Mode == BodyMode.Grounded;
        });

        Assert.True(crossedAbove);
        Assert.Equal(500, sim.Current.FeetPos.Y); // and landed on it on the way down
    }

    [Fact]
    public void Platforms_are_exposed_for_debugging()
    {
        var desktop = WithWindow(400, 500);
        var sim = NewSim(desktop);
        sim.Tick(Dt, desktop);
        Assert.Contains(sim.Platforms, p => p.Kind == PlatformKind.WindowTop);
        Assert.Contains(sim.Platforms, p => p.Kind == PlatformKind.Floor);
    }
}
