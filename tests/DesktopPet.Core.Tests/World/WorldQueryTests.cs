using DesktopPet.Core.Math;
using DesktopPet.Core.Tests.Fakes;
using DesktopPet.Core.World;

namespace DesktopPet.Core.Tests.World;

public class WorldQueryTests
{
    // Primary 1920x1080 at origin; a taller 2560x1440 monitor to the right, raised by 200px.
    //   y=-200            ┌──────────┐
    //   y=0    ┌───────┐  │    B     │
    //          │   A   │  │          │
    //   y=1080 └───────┘  │          │
    //   y=1240            └──────────┘
    private static readonly IReadOnlyList<MonitorInfo> Layout = new FakeDesktop()
        .Monitor(0, 0, 1920, 1080)
        .Monitor(1920, -200, 2560, 1440, scale: 1.5)
        .Snapshot().Monitors;

    [Theory]
    [InlineData(100, 500, false)]     // inside A
    [InlineData(3000, 1200, false)]   // inside B, below A's bottom
    [InlineData(100, -300, false)]    // above A: will fall back in
    [InlineData(100, 1081.5, true)]   // below A
    [InlineData(-50, 500, true)]      // left of everything
    [InlineData(5000, 500, true)]     // right of everything
    [InlineData(3000, 1300, true)]    // below B
    public void IsLost(double x, double y, bool expected) =>
        Assert.Equal(expected, WorldQuery.IsLost(Layout, new Vec2(x, y)));

    [Fact]
    public void Feet_on_bottom_edge_count_as_inside()
    {
        Assert.False(WorldQuery.IsLost(Layout, new Vec2(100, 1080)));
        Assert.Equal(Layout[0], WorldQuery.MonitorAt(Layout, new Vec2(100, 1080)));
    }

    [Fact]
    public void MonitorAt_and_Nearest()
    {
        Assert.Equal(Layout[1], WorldQuery.MonitorAt(Layout, new Vec2(2000, -100)));
        Assert.Null(WorldQuery.MonitorAt(Layout, new Vec2(100, 1200)));
        Assert.Equal(Layout[0], WorldQuery.Nearest(Layout, new Vec2(100, 1200)));
        Assert.Equal(Layout[1], WorldQuery.Nearest(Layout, new Vec2(6000, 0)));
    }

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
}
