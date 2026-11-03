using DesktopPet.Core.Tests.Fakes;
using DesktopPet.Core.World;

namespace DesktopPet.Core.Tests.World;

public class PlatformBuilderTests
{
    private static FakeDesktop Screen() => new FakeDesktop().Monitor(0, 0, 1920, 1080, taskbar: 48);

    private static List<Platform> WindowTops(DesktopSnapshot s) =>
        PlatformBuilder.Build(s).Where(p => p.Kind == PlatformKind.WindowTop).ToList();

    private static (double Left, double Right)[] Spans(DesktopSnapshot s, long id) =>
        WindowTops(s).Where(p => p.Key == PlatformBuilder.WindowKey(id)).Select(p => (p.Left, p.Right)).ToArray();

    [Fact]
    public void Floors_are_work_area_bottoms_keyed_by_monitor()
    {
        var snapshot = new FakeDesktop().Monitor(0, 0, 1920, 1080, taskbar: 48).Monitor(1920, 0, 1920, 1080).Snapshot();
        var floors = PlatformBuilder.Build(snapshot);

        Assert.Collection(
            floors,
            f => Assert.Equal(new Platform(PlatformKind.Floor, @"floor:\\.\DISPLAY1", 1032, 0, 1920), f),
            f => Assert.Equal(new Platform(PlatformKind.Floor, @"floor:\\.\DISPLAY2", 1080, 1920, 3840), f));
    }

    [Fact]
    public void Unobstructed_window_top_is_a_platform_anchored_to_the_window()
    {
        var s = Screen().Window(7, 200, 300, 800, 500).Snapshot();
        var top = Assert.Single(WindowTops(s));

        Assert.Equal(new Platform(PlatformKind.WindowTop, "window:7", 300, 200, 1000, AnchorX: 200, AnchorWidth: 800), top);
    }

    [Fact]
    public void Window_above_splits_the_top_edge_of_the_one_below()
    {
        var s = Screen()
            .Window(1, 400, 200, 200, 400)  // front: covers y=300 between x 400..600
            .Window(2, 200, 300, 800, 500)  // back
            .Snapshot();

        Assert.Equal([(200.0, 400.0), (600.0, 1000.0)], Spans(s, 2));
        Assert.Equal([(400.0, 600.0)], Spans(s, 1));
    }

    [Fact]
    public void Window_below_does_not_hide_the_one_above()
    {
        var s = Screen()
            .Window(1, 200, 300, 800, 500)  // front
            .Window(2, 400, 200, 200, 400)  // behind, its body crosses y=300
            .Snapshot();

        Assert.Equal([(200.0, 1000.0)], Spans(s, 1));
        Assert.Equal([(400.0, 600.0)], Spans(s, 2)); // its own top at y=200 is clear
    }

    [Fact]
    public void Window_above_that_ends_at_the_edge_does_not_hide_it()
    {
        // Front window's bottom row is y=299 (exclusive 300): the back window's top row y=300 is visible.
        var s = Screen().Window(1, 200, 100, 800, 200).Window(2, 200, 300, 800, 300).Snapshot();
        Assert.Equal([(200.0, 1000.0)], Spans(s, 2));
    }

    [Fact]
    public void Fully_covered_top_is_not_a_platform()
    {
        var s = Screen().Window(1, 100, 100, 1200, 800).Window(2, 200, 300, 800, 500).Snapshot();
        Assert.Empty(Spans(s, 2));
    }

    [Fact]
    public void Slivers_are_dropped()
    {
        var s = Screen()
            .Window(1, 200, 200, 795, 400)   // leaves 5 px of window 2's top uncovered on the right
            .Window(2, 200, 300, 800, 500)
            .Snapshot();
        Assert.Empty(Spans(s, 2));
    }

    [Fact]
    public void Maximized_windows_are_not_platforms_but_still_hide_windows_behind()
    {
        var s = Screen()
            .Window(1, 0, 0, 1920, 1032, maximized: true)
            .Window(2, 200, 300, 800, 500)
            .Snapshot();
        Assert.Empty(WindowTops(s));
    }

    [Fact]
    public void Top_at_the_very_top_of_the_screen_is_not_a_platform()
    {
        // The pet would stand with its whole body off-screen.
        var s = Screen().Window(1, 200, 0, 800, 500).Window(2, 200, -40, 800, 500).Snapshot();
        Assert.Empty(WindowTops(s));
    }

    [Fact]
    public void Off_screen_part_is_clipped()
    {
        var s = Screen().Window(1, -300, 400, 800, 300).Window(2, 1500, 500, 1000, 300).Snapshot();
        Assert.Equal([(0.0, 500.0)], Spans(s, 1));
        Assert.Equal([(1500.0, 1920.0)], Spans(s, 2));
    }

    [Fact]
    public void Top_inside_the_taskbar_band_is_not_a_platform()
    {
        var s = Screen().Window(1, 200, 1040, 800, 300).Snapshot(); // taskbar occupies 1032..1080
        Assert.Empty(WindowTops(s));
    }

    [Fact]
    public void Window_across_two_monitors_is_one_continuous_platform()
    {
        var s = new FakeDesktop().Monitor(0, 0, 1920, 1080).Monitor(1920, 0, 1920, 1080)
            .Window(1, 1500, 400, 1000, 300)
            .Snapshot();
        Assert.Equal([(1500.0, 2500.0)], Spans(s, 1));
    }

    [Fact]
    public void Window_across_a_taller_and_a_shorter_monitor_is_clipped_to_where_it_is_visible()
    {
        // B starts at y=500, so the top at y=400 only exists over A.
        var s = new FakeDesktop().Monitor(0, 0, 1920, 1080).Monitor(1920, 500, 1920, 1080)
            .Window(1, 1500, 400, 1000, 300)
            .Snapshot();
        Assert.Equal([(1500.0, 1920.0)], Spans(s, 1));
    }

    [Fact]
    public void Many_overlapping_windows()
    {
        //   z0: [300, 500)  at y 100..900
        //   z1: [700, 900)  at y 150..900
        //   z2: [100, 1100) at y 200 (target)
        var s = Screen()
            .Window(1, 300, 100, 200, 800)
            .Window(2, 700, 150, 200, 750)
            .Window(3, 100, 200, 1000, 600)
            .Snapshot();
        Assert.Equal([(100.0, 300.0), (500.0, 700.0), (900.0, 1100.0)], Spans(s, 3));
    }
}
