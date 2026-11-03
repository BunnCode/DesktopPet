using DesktopPet.Core.Animation;
using DesktopPet.Core.Math;
using DesktopPet.Core.Simulation;
using DesktopPet.Core.World;

namespace DesktopPet.Core.Tests.Simulation;

public class PetSimulationTests
{
    private static readonly MonitorInfo Left = new(
        new RectI(-1920, 0, 0, 1080), new RectI(-1920, 0, 0, 1080), Scale: 1.0, IsPrimary: false);

    private static readonly MonitorInfo Primary = new(
        new RectI(0, 0, 2560, 1440), new RectI(0, 0, 2560, 1392), Scale: 1.5, IsPrimary: true);

    [Fact]
    public void Spawns_bottom_centre_of_primary_work_area()
    {
        var sim = new PetSimulation(PetConfig.Default, [Left, Primary]);
        Assert.Equal(new Vec2(1280, 1392), sim.Current.FeetPos);
    }

    [Fact]
    public void Size_scales_with_monitor_dpi()
    {
        var sim = new PetSimulation(PetConfig.Default with { SizeDip = 64 }, [Primary]);
        Assert.Equal(new Vec2(96, 96), sim.Current.Size);
    }

    [Fact]
    public void Falls_back_to_first_monitor_without_primary_and_survives_no_monitors()
    {
        Assert.Equal(new Vec2(-960, 1080), new PetSimulation(PetConfig.Default, [Left]).Current.FeetPos);
        _ = new PetSimulation(PetConfig.Default, []);
    }

    [Fact]
    public void Sprite_rect_sits_on_feet_point()
    {
        var sim = new PetSimulation(PetConfig.Default, [Primary]);
        var r = sim.Current.Bounds;
        Assert.Equal(1392, r.Bottom); // exclusive bottom == work-area bottom: last sprite row is just above the taskbar
        Assert.Equal(96, r.Width);
        Assert.Equal(96, r.Height);
        Assert.Equal(1280, r.Left + (r.Width / 2));
    }

    [Fact]
    public void Tick_plays_idle_and_tracks_previous_frame()
    {
        var sim = new PetSimulation(PetConfig.Default, [Primary]);
        Assert.Equal(Animations.Idle.Id, sim.Current.AnimationId);

        var frameTime = 1.0 / Animations.Idle.Fps;
        sim.Tick(frameTime * 1.01);

        Assert.Equal(0, sim.Previous.FrameIndex);
        Assert.Equal(1, sim.Current.FrameIndex);
    }
}
