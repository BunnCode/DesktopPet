using DesktopPet.Core.Behavior;
using DesktopPet.Core.Math;
using DesktopPet.Core.Physics;
using DesktopPet.Core.Simulation;
using DesktopPet.Core.Tests.Fakes;
using static DesktopPet.Core.Tests.Fakes.SimulationExtensions;

namespace DesktopPet.Core.Tests.Simulation;

/// <summary>Which window a pet can read (M12b): the one it's on, clings to, or stands beside.</summary>
public class ReadableWindowTests
{
    private const long Win = 7;
    private const double Reach = 40;
    private static readonly double HalfSize = PetConfig.Default.SizeDip / 2;

    // A window standing on the floor (1040), from x 800 to 1200.
    private static readonly Core.World.DesktopSnapshot Desktop =
        new FakeDesktop().Monitor(0, 0, 1920, 1080, taskbar: 40).Window(Win, 800, 600, 400, 440).Snapshot();

    private static PetSimulation LandedAt(double x, Core.Abstractions.IRandom? random = null)
    {
        var sim = NewSim(Desktop, random);
        sim.Place(new Vec2(x, 100), Vec2.Zero);
        sim.RunUntil(Desktop, Grounded);
        return sim;
    }

    [Fact]
    public void Standing_on_a_window_reads_that_window()
    {
        var sim = LandedAt(1000);

        Assert.Equal(Win, sim.ReadableWindow(Reach));
    }

    [Fact]
    public void Standing_on_the_floor_right_beside_a_window_reads_it()
    {
        var sim = LandedAt(800 - HalfSize - 10); // its box ends 10 DIP short of the window's left side

        Assert.Equal(Win, sim.ReadableWindow(Reach));
    }

    [Fact]
    public void Standing_on_the_floor_away_from_windows_reads_nothing()
    {
        var sim = LandedAt(300);

        Assert.Null(sim.ReadableWindow(Reach));
    }

    [Fact]
    public void Standing_in_front_of_a_window_reads_it()
    {
        // A maximized window can't be stood on: the pet lands on the taskbar in front of it.
        var desktop = new FakeDesktop().Monitor(0, 0, 1920, 1080, taskbar: 40).Window(Win, 0, 0, 1920, 1040, maximized: true).Snapshot();
        var sim = NewSim(desktop);
        sim.Place(new Vec2(500, 100), Vec2.Zero);
        sim.RunUntil(desktop, Grounded);

        Assert.Equal(1040, sim.Current.FeetPos.Y);
        Assert.Equal(Win, sim.ReadableWindow(Reach));
    }

    [Fact]
    public void In_front_of_one_window_and_beside_another_reads_the_one_behind_it()
    {
        const long behind = 9;
        var desktop = new FakeDesktop().Monitor(0, 0, 1920, 1080, taskbar: 40)
            .Window(Win, 800, 600, 400, 440)                     // in front, its side close to the pet
            .Window(behind, 0, 0, 1920, 1040, maximized: true)    // behind everything
            .Snapshot();
        var sim = NewSim(desktop);
        sim.Place(new Vec2(800 - HalfSize - 10, 100), Vec2.Zero);
        sim.RunUntil(desktop, Grounded);

        Assert.Equal(behind, sim.ReadableWindow(Reach));
    }

    [Fact]
    public void The_frontmost_window_behind_it_is_the_one_read()
    {
        const long back = 9;
        var desktop = new FakeDesktop().Monitor(0, 0, 1920, 1080, taskbar: 40)
            .Window(Win, 0, 500, 1000, 540, maximized: true) // front (not standable, so the pet lands on the taskbar)
            .Window(back, 0, 0, 1920, 1040, maximized: true)
            .Snapshot();
        var sim = NewSim(desktop);
        sim.Place(new Vec2(500, 100), Vec2.Zero);
        sim.RunUntil(desktop, Grounded);

        Assert.Equal(Win, sim.ReadableWindow(Reach));
    }

    [Fact]
    public void The_window_behind_is_the_one_it_stands_in_front_of_not_on()
    {
        var desktop = new FakeDesktop().Monitor(0, 0, 1920, 1080, taskbar: 40).Window(Win, 0, 0, 1920, 1040, maximized: true).Snapshot();
        var inFront = NewSim(desktop);
        inFront.Place(new Vec2(500, 100), Vec2.Zero);
        inFront.RunUntil(desktop, Grounded);

        Assert.Equal(Win, inFront.WindowBehind());
        Assert.Null(LandedAt(1000).WindowBehind()); // standing on it
        Assert.Null(LandedAt(300).WindowBehind());  // nothing behind
    }

    [Fact]
    public void On_top_of_one_window_in_front_of_another_the_other_is_behind()
    {
        const long back = 9;
        var desktop = new FakeDesktop().Monitor(0, 0, 1920, 1080, taskbar: 40)
            .Window(Win, 800, 600, 400, 440)                // stood on
            .Window(back, 0, 0, 1920, 1040, maximized: true) // behind it
            .Snapshot();
        var sim = NewSim(desktop);
        sim.Place(new Vec2(1000, 100), Vec2.Zero);
        sim.RunUntil(desktop, Grounded);

        Assert.Equal(600, sim.Current.FeetPos.Y);
        Assert.Null(sim.WindowBehind()); // it stands on Win: notes go there
    }

    [Fact]
    public void Falling_pets_read_nothing()
    {
        var sim = NewSim(Desktop);
        sim.Place(new Vec2(1000, 100), Vec2.Zero);
        sim.Tick(Dt, Desktop);

        Assert.Equal(BodyMode.Airborne, sim.Mode);
        Assert.Null(sim.ReadableWindow(Reach));
    }

    [Fact]
    public void Climbing_a_window_side_reads_that_window()
    {
        // Walk.Enter rolls duration then "turn?" (0.99 = keep going); 0.0 at the wall means climb.
        var sim = LandedAt(500, new ScriptedRandom(0.0, 0.99, 0.99, 0.0));
        sim.ForceState(PetStateId.Walk, facingLeft: false);
        sim.RunUntil(Desktop, s => s.Mode == BodyMode.Clinging, maxTicks: 600);

        Assert.Equal(Win, sim.ReadableWindow(reachDip: 0));
    }
}
