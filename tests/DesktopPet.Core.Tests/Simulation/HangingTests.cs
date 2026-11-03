using DesktopPet.Core.Behavior;
using DesktopPet.Core.Math;
using DesktopPet.Core.Physics;
using DesktopPet.Core.Simulation;
using DesktopPet.Core.Tests.Fakes;
using DesktopPet.Core.World;
using static DesktopPet.Core.Tests.Fakes.SimulationExtensions;

namespace DesktopPet.Core.Tests.Simulation;

/// <summary>Hanging from the title bars of maximized windows (behind <see cref="PetConfig.EnableHanging"/>, off by default).</summary>
public class HangingTests
{
    private const long Editor = 7;
    private static readonly PetConfig Config = PetConfig.Default with { EnableHanging = true };
    private static readonly double Size = Config.SizeDip; // scale 1

    private static PetSimulation Sim(DesktopSnapshot desktop, Abstractions.IRandom random) => NewSim(desktop, random, Config);

    // ---- Disabled (the default) ----

    [Fact]
    public void Hanging_is_off_by_default()
    {
        Assert.False(PetConfig.Default.EnableHanging);

        var sim = NewSim(Maximized(), new FakeRandom(0.0)); // default config
        Assert.DoesNotContain(sim.Platforms, p => p.Kind == PlatformKind.HangBar);
        Assert.Equal(BodyMode.Airborne, sim.Mode); // spawns falling, not hanging

        sim.RunUntil(Maximized(), Grounded);
        Assert.Equal(1040, sim.Current.FeetPos.Y);
    }

    [Fact]
    public void When_off_thrown_upward_past_a_title_bar_does_not_catch()
    {
        var sim = NewSim(Maximized(), new FakeRandom(0.0));
        sim.Place(new Vec2(800, 700), new Vec2(200, -2400));
        sim.RunUntil(Maximized(), Grounded, maxTicks: 300);
        Assert.Equal(0, sim.RespawnCount);
    }

    [Fact]
    public void Builder_can_omit_hang_bars()
    {
        Assert.DoesNotContain(PlatformBuilder.Build(Maximized(), includeHangBars: false), p => p.Kind == PlatformKind.HangBar);
    }

    // ---- Enabled ----

    // A 1920×1080 screen with a 40 px taskbar and a maximized window filling the work area: bar at y = 0 + 32.
    private static FakeDesktop Screen() => new FakeDesktop().Monitor(0, 0, 1920, 1080, taskbar: 40);

    private static DesktopSnapshot Maximized() => Screen().Window(Editor, 0, 0, 1920, 1040, maximized: true).Snapshot();

    private static double BarY => Config.CaptionHeightDip;

    // ---- Geometry ----

    [Fact]
    public void Maximized_window_gets_a_hang_bar_below_its_title_bar_and_no_top_platform()
    {
        var platforms = PlatformBuilder.Build(Maximized());

        var bar = Assert.Single(platforms, p => p.Kind == PlatformKind.HangBar);
        Assert.Equal(new Platform(PlatformKind.HangBar, "hang:7", 32, 0, 1920, AnchorX: 0, AnchorWidth: 1920), bar);
        Assert.DoesNotContain(platforms, p => p.Kind == PlatformKind.WindowTop);
    }

    [Fact]
    public void Hang_bar_height_scales_with_dpi()
    {
        var s = new FakeDesktop().Monitor(0, 0, 2560, 1440, scale: 1.25, taskbar: 60)
            .Window(1, 0, 0, 2560, 1380, maximized: true).Snapshot();
        Assert.Equal(40, Assert.Single(PlatformBuilder.Build(s), p => p.Kind == PlatformKind.HangBar).Y);
    }

    [Fact]
    public void Unmaximized_window_pinned_to_the_screen_top_gets_a_hang_bar()
    {
        var s = Screen().Window(1, 300, 0, 800, 600).Snapshot();
        var bar = Assert.Single(PlatformBuilder.Build(s), p => p.Kind == PlatformKind.HangBar);
        Assert.Equal((300.0, 1100.0, 32.0), (bar.Left, bar.Right, bar.Y));
    }

    [Fact]
    public void Ordinary_window_gets_a_top_platform_not_a_hang_bar()
    {
        var s = Screen().Window(1, 300, 200, 800, 600).Snapshot();
        Assert.DoesNotContain(PlatformBuilder.Build(s), p => p.Kind == PlatformKind.HangBar);
    }

    [Fact]
    public void Window_in_front_covering_the_title_bar_hides_the_bar_there()
    {
        var s = Screen()
            .Window(1, 600, 10, 400, 300)                       // in front, covers the title bar between 600 and 1000
            .Window(Editor, 0, 0, 1920, 1040, maximized: true)
            .Snapshot();

        var bars = PlatformBuilder.Build(s).Where(p => p.Kind == PlatformKind.HangBar).Select(p => (p.Left, p.Right));
        Assert.Equal([(0.0, 600.0), (1000.0, 1920.0)], bars);
    }

    [Fact]
    public void Falling_body_does_not_land_on_a_hang_bar()
    {
        var sim = Sim(Screen().Snapshot(), new FakeRandom(0.0));
        var desktop = Maximized();
        sim.Place(new Vec2(500, 10), Vec2.Zero); // feet above the bar, falling down through it
        sim.RunUntil(desktop, Grounded);
        Assert.Equal(1040, sim.Current.FeetPos.Y);
    }

    // ---- Getting onto a bar ----

    [Fact]
    public void Spawns_hanging_when_a_maximized_window_is_there()
    {
        var sim = Sim(Maximized(), new FakeRandom(0.0));

        Assert.Equal(BodyMode.Hanging, sim.Mode);
        Assert.Equal(PetStateId.Hang, sim.State);
        Assert.Equal(new Vec2(960, BarY + Size), sim.Current.FeetPos);
        Assert.Equal(BarY, sim.Current.Bounds.Top); // hands at the bar, body below: fully on screen
    }

    [Fact]
    public void Released_by_the_mouse_near_a_bar_grabs_it()
    {
        var desktop = Maximized();
        var sim = Sim(desktop, new FakeRandom(0.0));
        sim.Place(new Vec2(500, 600), Vec2.Zero);
        sim.RunUntil(desktop, Grounded);

        // Drag up so the top of the pet is just under the bar, hold still, let go.
        var t = 10.0;
        sim.PointerDown(new Vec2(500, 1020), t);
        var target = new Vec2(500, 1020 - (1040 - (BarY + 10 + Size)));
        foreach (var p in new[] { new Vec2(500, 1010), target, target, target, target, target, target, target, target })
        {
            sim.PointerMove(p, t += Dt);
            sim.Tick(Dt, desktop);
        }

        sim.PointerUp(target, t);

        Assert.Equal(BodyMode.Hanging, sim.Mode);
        Assert.Equal(PetStateId.Hang, sim.State);
        sim.Tick(Dt, desktop);
        Assert.Equal(BarY, sim.Current.Bounds.Top);
    }

    [Fact]
    public void Released_far_from_a_bar_just_falls()
    {
        var desktop = Maximized();
        var sim = Sim(desktop, new FakeRandom(0.0));
        sim.Place(new Vec2(500, 600), Vec2.Zero);
        sim.RunUntil(desktop, Grounded);

        var t = 10.0;
        sim.PointerDown(new Vec2(500, 1020), t);
        sim.PointerMove(new Vec2(500, 700), t += Dt);
        sim.PointerUp(new Vec2(500, 700), t + 0.2);

        Assert.Equal(BodyMode.Airborne, sim.Mode);
    }

    [Fact]
    public void Thrown_upward_into_a_bar_catches_it()
    {
        var desktop = Maximized();
        var sim = Sim(desktop, new FakeRandom(0.0));
        sim.Place(new Vec2(800, 700), new Vec2(200, -2400));

        sim.RunUntil(desktop, s => s.Mode != BodyMode.Airborne, maxTicks: 120);

        Assert.Equal(BodyMode.Hanging, sim.Mode);
        Assert.Equal(PetStateId.Hang, sim.State);
        Assert.Equal(BarY + Size, sim.Current.FeetPos.Y);
    }

    // ---- While hanging ----

    [Fact]
    public void Hanging_pet_rides_the_window_and_falls_when_it_is_restored()
    {
        var sim = Sim(Maximized(), new FakeRandom(0.0));

        // Restored: the window is now an ordinary window lower down; its title bar is no longer a hang bar.
        var restored = Screen().Window(Editor, 200, 300, 1200, 600).Snapshot();
        sim.Tick(Dt, restored);
        Assert.Equal(PetStateId.Fall, sim.State);

        sim.RunUntil(restored, Grounded);
        Assert.Equal(300, sim.Current.FeetPos.Y); // landed on the restored window's top
    }

    [Fact]
    public void Hanging_pet_falls_when_the_window_is_minimized()
    {
        var sim = Sim(Maximized(), new FakeRandom(0.0));
        var minimized = Screen().Snapshot();
        sim.RunUntil(minimized, Grounded);
        Assert.Equal(1040, sim.Current.FeetPos.Y);
    }

    [Fact]
    public void Eventually_lets_go()
    {
        // Every roll 0.5: the hang lasts 5.5 s, then 0.5 lands in the drop band [0.35, 0.65).
        var sim = Sim(Maximized(), new FakeRandom(0.5));
        sim.RunUntil(Maximized(), s => s.State == PetStateId.Fall, maxTicks: (int)((Config.HangSeconds.Max + 1) / Dt));
        sim.RunUntil(Maximized(), Grounded);
        Assert.Equal(1040, sim.Current.FeetPos.Y);
    }

    [Fact]
    public void Shimmies_along_the_bar_and_turns_before_running_out()
    {
        // Narrow bar: a window pinned to the top, 300 px wide.
        var desktop = Screen().Window(1, 800, 0, 300, 600).Snapshot();
        var sim = Sim(desktop, new FakeRandom(0.99));
        sim.Place(new Vec2(950, 200), new Vec2(0, -2000));
        sim.RunUntil(desktop, s => s.Mode == BodyMode.Hanging, maxTicks: 60);

        sim.ForceState(PetStateId.Shimmy, facingLeft: false);
        var xs = new List<double>();
        for (var i = 0; i < (int)(4 / Dt); i++)
        {
            sim.Tick(Dt, desktop);
            xs.Add(sim.Current.FeetPos.X);
            Assert.Equal(BodyMode.Hanging, sim.Mode);
        }

        Assert.True(xs.Max() > 1000);           // moved
        Assert.All(xs, x => Assert.InRange(x, 800, 1100));
        Assert.Contains(xs.Zip(xs.Skip(1)), p => p.Second < p.First); // turned around at least once
    }

    [Fact]
    public void Can_be_picked_off_the_bar()
    {
        var desktop = Maximized();
        var sim = Sim(desktop, new FakeRandom(0.0));
        var feet = sim.Current.FeetPos;

        sim.PointerDown(feet - new Vec2(0, 20), 1);
        sim.PointerMove(feet + new Vec2(0, 300), 1.05);
        Assert.True(sim.IsHeld);

        sim.PointerUp(feet + new Vec2(0, 300), 1.3);
        Assert.Equal(BodyMode.Airborne, sim.Mode);
    }
}
