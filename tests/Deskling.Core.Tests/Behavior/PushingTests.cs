using Deskling.Core.Behavior;
using Deskling.Core.Math;
using Deskling.Core.Simulation;
using Deskling.Core.Tests.Fakes;
using Deskling.Core.World;
using static Deskling.Core.Tests.Fakes.SimulationExtensions;

namespace Deskling.Core.Tests.Behavior;

/// <summary>Pushing real windows (via <see cref="WindowCommand"/>s the host carries out).</summary>
public class PushingTests
{
    private const long Win = 5;
    private static readonly PetConfig Config = PetConfig.Default;
    private static readonly double HalfWidth = Config.SizeDip * Config.BodyHalfWidthFraction;

    private static DesktopSnapshot Desktop(int windowX, int width = 400) =>
        new FakeDesktop().Monitor(0, 0, 1920, 1080, taskbar: 40).Window(Win, windowX, 600, width, 440).Snapshot();

    /// <summary>Walks right into the window's left side; rolls: duration, keep heading, then 0.5 = push (0.35 ≤ 0.5 &lt; 0.6).</summary>
    private static PetSimulation WalkInto(int windowX, out DesktopSnapshot desktop, int width = 400)
    {
        desktop = Desktop(windowX, width);
        var sim = NewSim(desktop, new ScriptedRandom(0.99, 0.99, 0.99, 0.5));
        sim.Place(new Vec2(windowX - 100, 900), Vec2.Zero);
        sim.RunUntil(desktop, Grounded);
        sim.ForceState(PetStateId.Walk, facingLeft: false);
        var snapshot = desktop;
        sim.RunUntil(snapshot, s => s.State == PetStateId.Push, maxTicks: 200);
        return sim;
    }

    [Fact]
    public void Pushes_the_window_along_and_stays_against_it()
    {
        var sim = WalkInto(800, out _);
        var x = 800.0;
        var moved = 0;

        // Act as the host: carry out each frame's commands, then show Core the moved window.
        for (var i = 0; i < 60 && sim.State == PetStateId.Push; i++)
        {
            foreach (var c in sim.TakeCommands())
            {
                Assert.Equal(Win, c.WindowId);
                Assert.Equal(0, c.Dy);
                x += c.Dx;
                moved += c.Dx;
            }

            sim.Tick(Dt, Desktop((int)x));
        }

        Assert.InRange(moved, 25, 35);                           // ~30 DIP/s for one second
        Assert.Equal(x - HalfWidth, sim.Current.FeetPos.X, 0);   // still leaning on it
        Assert.False(sim.Current.FacingLeft);
    }

    [Fact]
    public void Gives_up_on_a_window_that_will_not_move()
    {
        var sim = WalkInto(800, out var desktop);
        var commands = 0;
        sim.RunUntil(desktop, s =>
        {
            commands += s.TakeCommands().Count;
            return s.State != PetStateId.Push;
        }, maxTicks: 120);

        Assert.True(commands > 0);
        Assert.Equal(PetStateId.Walk, sim.State);
        Assert.True(sim.Current.FacingLeft || sim.State == PetStateId.Walk); // turned away

        // It won't try to push that window again: walking back into it never yields Push.
        sim.ForceState(PetStateId.Walk, facingLeft: false);
        var states = new HashSet<PetStateId>();
        for (var i = 0; i < 120; i++)
        {
            sim.Tick(Dt, desktop);
            states.Add(sim.State);
        }

        Assert.DoesNotContain(PetStateId.Push, states);
    }

    [Fact]
    public void Never_pushes_a_window_off_the_screen()
    {
        var sim = WalkInto(1520, out var desktop); // right edge already at the screen edge (1920)
        sim.Tick(Dt, desktop);
        Assert.Empty(sim.TakeCommands());
        Assert.NotEqual(PetStateId.Push, sim.State);
    }

    [Fact]
    public void Pushing_toward_the_edge_with_a_slow_host_stays_on_screen()
    {
        // Window ends 120 px short of the edge. The host only reports the new position every 3rd tick (stale
        // snapshots), and enforces the guard like the real platform layer does.
        var sim = WalkInto(1400, out _);
        var frame = RectI.FromSize(1400, 600, 400, 440);
        var work = new RectI(0, 0, 1920, 1040);
        var snapshot = Desktop(frame.Left);

        for (var i = 0; i < 60 * 8; i++)
        {
            foreach (var c in sim.TakeCommands())
            {
                var (dx, dy) = WindowMoveGuard.Clamp(frame, work, c.Dx, c.Dy);
                frame = frame.Offset(dx, dy);
                Assert.True(frame.Right <= 1920, $"window pushed to {frame.Right}");
            }

            if (i % 3 == 0)
            {
                snapshot = Desktop(frame.Left);
            }

            sim.Tick(Dt, snapshot);
        }

        Assert.True(frame.Right <= 1920);
        Assert.True(frame.Right > 1900); // it did push it most of the way
    }

    [Fact]
    public void Switching_pushing_off_mid_push_stops_it()
    {
        var sim = WalkInto(800, out var desktop);
        sim.PushingEnabled = false;
        sim.Tick(Dt, desktop);
        sim.TakeCommands();

        Assert.NotEqual(PetStateId.Push, sim.State);
        sim.Run(desktop, 30);
        Assert.Empty(sim.TakeCommands());
    }

    [Fact]
    public void Pushing_disabled_never_moves_windows()
    {
        var desktop = Desktop(800);
        var sim = NewSim(desktop, new ScriptedRandom(0.99, 0.99, 0.99, 0.5), Config with { EnablePushing = false });
        sim.Place(new Vec2(700, 900), Vec2.Zero);
        sim.RunUntil(desktop, Grounded);
        sim.ForceState(PetStateId.Walk, facingLeft: false);

        for (var i = 0; i < 180; i++)
        {
            sim.Tick(Dt, desktop);
            Assert.Empty(sim.TakeCommands());
        }

        Assert.NotEqual(PetStateId.Push, sim.State);
    }
}
