using Deskling.Core.Furniture;
using Deskling.Core.Simulation;
using Deskling.Core.Tests.Fakes;
using Deskling.Core.World;

namespace Deskling.Core.Tests.Furniture;

/// <summary>Finding a way to furniture across surfaces with the moves a pet already makes.</summary>
public class RoutePlannerTests
{
    private const long Win = 5;
    private static readonly PetConfig Config = PetConfig.Default;
    private static readonly double HalfWidth = Config.SizeDip * Config.BodyHalfWidthFraction;
    private static readonly Walker Climber = new(HalfWidth, Config.SizeDip, 1, CanClimb: true, Config.GravityDip);
    private static readonly Walker NonClimber = Climber with { CanClimb = false };

    private static readonly string Floor = PlatformBuilder.FloorKey(new FakeDesktop().Monitor(0, 0, 1, 1).Snapshot().Monitors[0]);
    private static readonly string Window = PlatformBuilder.WindowKey(Win);

    /// <summary>A window standing on the floor (1040), its top at 700 from x 600 to 1000.</summary>
    private static DesktopSnapshot Standing() =>
        new FakeDesktop().Monitor(0, 0, 1920, 1080, taskbar: 40).Window(Win, 600, 700, 400, 340).Snapshot();

    private static IReadOnlyList<RouteStep>? Plan(DesktopSnapshot desktop, string from, double fromX, string to, double toX, Walker walker) =>
        RoutePlanner.Plan(
            PlatformBuilder.Build(desktop, Config.CaptionHeightDip, includeHangBars: false), WallBuilder.Build(desktop), desktop.Monitors,
            from, fromX, to, toX, walker);

    [Fact]
    public void On_the_same_surface_it_just_walks_there()
    {
        var route = Plan(Standing(), Floor, 100, Floor, 400, Climber);
        Assert.Equal([new RouteStep(RouteMove.Arrive, 400)], route);
    }

    [Fact]
    public void Up_onto_a_window_it_climbs_the_nearer_side()
    {
        var route = Plan(Standing(), Floor, 300, Window, 800, Climber)!;

        Assert.Equal(2, route.Count);
        Assert.Equal(RouteMove.ClimbUp, route[0].Move);
        Assert.Equal(600 - HalfWidth, route[0].X, 6);
        Assert.Equal(WallBuilder.SideKey(Win, "L"), route[0].WallKey);
        Assert.Equal(new RouteStep(RouteMove.Arrive, 800), route[1]);

        var fromRight = Plan(Standing(), Floor, 1500, Window, 800, Climber)!;
        Assert.Equal(1000 + HalfWidth, fromRight[0].X, 6);
    }

    [Fact]
    public void Down_from_a_window_it_climbs_down_the_side()
    {
        var route = Plan(Standing(), Window, 650, Floor, 300, Climber)!;

        Assert.Equal(RouteMove.ClimbDown, route[0].Move);
        Assert.Equal(-1, route[0].Direction);
        Assert.Equal(600.5, route[0].X);
        Assert.Equal(new RouteStep(RouteMove.Arrive, 300), route[^1]);
    }

    [Fact]
    public void Without_climbing_it_drops_off_the_end_but_cant_get_up()
    {
        var down = Plan(Standing(), Window, 650, Floor, 300, NonClimber)!;
        Assert.Equal(RouteMove.WalkOff, down[0].Move);
        Assert.Equal(599.5, down[0].X);

        Assert.Null(Plan(Standing(), Floor, 300, Window, 800, NonClimber));
    }

    [Fact]
    public void A_floating_window_can_only_be_dropped_from()
    {
        var floating = new FakeDesktop().Monitor(0, 0, 1920, 1080, taskbar: 40).Window(Win, 600, 300, 400, 200).Snapshot();
        Assert.Null(Plan(floating, Floor, 300, Window, 800, Climber)); // its sides end in mid-air
        Assert.NotNull(Plan(floating, Window, 800, Floor, 300, Climber));
    }

    [Fact]
    public void Across_a_monitor_seam_at_the_same_height_it_walks_on()
    {
        var two = new FakeDesktop().Monitor(0, 0, 1920, 1080, taskbar: 40).Monitor(1920, 0, 1920, 1080, taskbar: 40).Snapshot();
        var second = PlatformBuilder.FloorKey(two.Monitors[1]);

        var route = Plan(two, Floor, 1800, second, 2200, Climber)!;

        Assert.Equal(RouteMove.WalkOff, route[0].Move);
        Assert.Equal(1, route[0].Direction);
        Assert.Equal(new RouteStep(RouteMove.Arrive, 2200), route[^1]);
    }

    [Fact]
    public void Onto_a_window_on_top_of_a_window_it_climbs_twice()
    {
        // A small window standing on a bigger one that stands on the floor.
        var desktop = new FakeDesktop().Monitor(0, 0, 1920, 1080, taskbar: 40)
            .Window(7, 700, 500, 200, 200)   // top 500, bottom 700: stands on window 5
            .Window(Win, 600, 700, 400, 340)
            .Snapshot();

        var route = Plan(desktop, Floor, 300, PlatformBuilder.WindowKey(7), 800, Climber)!;

        Assert.Equal([RouteMove.ClimbUp, RouteMove.ClimbUp, RouteMove.Arrive], route.Select(s => s.Move));
        Assert.Equal(WallBuilder.SideKey(7, "L"), route[1].WallKey);
    }
}
