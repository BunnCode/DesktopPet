using Deskling.Core.Behavior;
using Deskling.Core.Math;
using Deskling.Core.Physics;
using Deskling.Core.Simulation;
using Deskling.Core.Tests.Fakes;
using Deskling.Core.World;
using static Deskling.Core.Tests.Fakes.SimulationExtensions;

namespace Deskling.Core.Tests.Behavior;

public class WalkingTests
{
    private static readonly double HalfWidth = PetConfig.Default.SizeDip * PetConfig.Default.BodyHalfWidthFraction; // at scale 1

    /// <summary>
    /// Drops the pet at <paramref name="x"/> above the floor under it, waits for it to land, then makes it walk.
    /// Random is fixed at 0.99: the walk lasts ~7 s and never randomly turns, so only walls change direction.
    /// </summary>
    private static PetSimulation WalkingPet(DesktopSnapshot desktop, double x, double dropFromY, bool facingLeft)
    {
        var sim = NewSim(desktop, new FakeRandom(0.99));
        sim.Place(new Vec2(x, dropFromY), Vec2.Zero);
        sim.RunUntil(desktop, Grounded);
        sim.ForceState(PetStateId.Walk, facingLeft);
        return sim;
    }

    /// <summary>Runs and records every frame.</summary>
    private static List<PetFrame> Record(PetSimulation sim, DesktopSnapshot desktop, double seconds)
    {
        var frames = new List<PetFrame>();
        for (var i = 0; i < (int)(seconds / Dt); i++)
        {
            sim.Tick(Dt, desktop);
            frames.Add(sim.Current);
        }

        return frames;
    }

    [Fact]
    public void Walks_at_configured_speed()
    {
        var desktop = new FakeDesktop().Monitor(0, 0, 1920, 1080).Snapshot();
        var sim = WalkingPet(desktop, 960, 500, facingLeft: false);

        sim.RunSeconds(desktop, 1.0);

        Assert.Equal(960 + PetConfig.Default.WalkSpeedDip, sim.Current.FeetPos.X, 1);
        Assert.Equal(PetStateId.Walk, sim.State);
        Assert.Equal(Core.Animation.Animations.Walk.Id, sim.Current.AnimationId);
    }

    [Fact]
    public void Walk_speed_scales_with_dpi()
    {
        var desktop = new FakeDesktop().Monitor(0, 0, 2560, 1440, scale: 1.5).Snapshot();
        var sim = WalkingPet(desktop, 1280, 500, facingLeft: true);

        sim.RunSeconds(desktop, 1.0);

        Assert.Equal(1280 - (PetConfig.Default.WalkSpeedDip * 1.5), sim.Current.FeetPos.X, 1);
        Assert.True(sim.Current.FacingLeft);
    }

    [Theory]
    [InlineData(false, 1850)]
    [InlineData(true, 70)]
    public void Turns_around_at_screen_edge_without_leaving_it(bool facingLeft, double startX)
    {
        var desktop = new FakeDesktop().Monitor(0, 0, 1920, 1080).Snapshot();
        var sim = WalkingPet(desktop, startX, 500, facingLeft);

        var frames = Record(sim, desktop, 3);

        Assert.All(frames, f => Assert.InRange(f.FeetPos.X, HalfWidth - 1, 1920 - HalfWidth + 1));
        Assert.Contains(frames, f => f.FacingLeft != facingLeft); // it turned
        Assert.All(frames, f => Assert.Equal(PetStateId.Walk, f.State));
    }

    [Fact]
    public void Side_taskbar_is_a_wall()
    {
        var desktop = new FakeDesktop()
            .Monitor(0, 0, 1920, 1080, workArea: new RectI(60, 0, 1920, 1080)) // taskbar docked left
            .Snapshot();
        var sim = WalkingPet(desktop, 120, 500, facingLeft: true);

        var frames = Record(sim, desktop, 3);

        Assert.All(frames, f => Assert.True(f.FeetPos.X >= 60 + HalfWidth - 1));
        Assert.Contains(frames, f => !f.FacingLeft);
    }

    [Fact]
    public void Crosses_into_same_height_neighbour_without_falling()
    {
        var desktop = new FakeDesktop().Monitor(0, 0, 1920, 1080).Monitor(1920, 0, 1920, 1080).Snapshot();
        var sim = WalkingPet(desktop, 1880, 500, facingLeft: false);

        var frames = Record(sim, desktop, 2);

        Assert.All(frames, f => Assert.Equal(PetStateId.Walk, f.State));
        Assert.All(frames, f => Assert.Equal(1080, f.FeetPos.Y));
        Assert.True(sim.Current.FeetPos.X > 1920);
        Assert.Equal(BodyMode.Grounded, sim.Mode);
    }

    [Fact]
    public void Size_changes_at_mixed_dpi_crossing_and_stays_on_the_floor()
    {
        var desktop = new FakeDesktop()
            .Monitor(0, 0, 1920, 1080, scale: 1.0)
            .Monitor(1920, 0, 1920, 1080, scale: 1.5)
            .Snapshot();
        var sim = WalkingPet(desktop, 1880, 500, facingLeft: false);

        var frames = Record(sim, desktop, 2);

        Assert.All(frames.Where(f => f.FeetPos.X < 1920), f => Assert.Equal(64, f.Size.X));
        Assert.All(frames.Where(f => f.FeetPos.X >= 1920), f => Assert.Equal(128, f.Size.X));
        Assert.All(frames, f => Assert.Equal(1080, f.Bounds.Bottom)); // anchored at the feet: never pops through the floor
    }

    [Fact]
    public void Drops_onto_neighbour_with_lower_floor()
    {
        var desktop = new FakeDesktop()
            .Monitor(0, 0, 1920, 1080, taskbar: 48)   // floor 1032
            .Monitor(1920, 0, 1920, 1080)             // floor 1080
            .Snapshot();
        var sim = WalkingPet(desktop, 1880, 500, facingLeft: false);

        sim.RunUntil(desktop, s => s.State == PetStateId.Fall, maxTicks: 120);
        Assert.True(sim.Current.FeetPos.X >= 1920);

        sim.RunUntil(desktop, Grounded, maxTicks: 60);
        Assert.Equal(1080, sim.Current.FeetPos.Y);
    }

    [Fact]
    public void Neighbour_with_higher_floor_is_a_wall()
    {
        var desktop = new FakeDesktop()
            .Monitor(0, 0, 1920, 1080)                // floor 1080
            .Monitor(1920, 0, 1920, 1080, taskbar: 48) // floor 1032: can't step up
            .Snapshot();
        var sim = WalkingPet(desktop, 1850, 500, facingLeft: false);

        var frames = Record(sim, desktop, 3);

        Assert.All(frames, f => Assert.True(f.FeetPos.X <= 1920 - HalfWidth + 1));
        Assert.Contains(frames, f => f.FacingLeft);
        Assert.All(frames, f => Assert.Equal(PetStateId.Walk, f.State));
    }

    [Fact]
    public void L_shaped_layout_walk_off_the_top_monitor_and_cannot_walk_back()
    {
        // A on top-left; B to the right and 540px lower, so its floor (1620) is below A's (1080).
        var desktop = new FakeDesktop()
            .Monitor(0, 0, 1920, 1080)
            .Monitor(1920, 540, 1920, 1080)
            .Snapshot();
        var sim = WalkingPet(desktop, 1880, 500, facingLeft: false);

        sim.RunUntil(desktop, s => s.State == PetStateId.Fall, maxTicks: 120);
        sim.RunUntil(desktop, Grounded, maxTicks: 120);
        Assert.Equal(1620, sim.Current.FeetPos.Y);

        // It stepped off A's edge with its centre just past 1920, so it lands overlapping the wall a little.
        // Told to walk back left, it must not get any further left; it turns and walks away instead.
        var landedX = sim.Current.FeetPos.X;
        Assert.InRange(landedX, 1920, 1930);

        sim.ForceState(PetStateId.Walk, facingLeft: true);
        var frames = Record(sim, desktop, 3);
        Assert.All(frames, f => Assert.True(f.FeetPos.X >= landedX));
        Assert.Contains(frames, f => !f.FacingLeft);
        Assert.True(frames[^1].FeetPos.X > 1920 + HalfWidth); // walked clear of the wall
        Assert.All(frames, f => Assert.Equal(1620, f.FeetPos.Y));
    }
}
