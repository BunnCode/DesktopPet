using DesktopPet.Core.Tests.Fakes;
using DesktopPet.Core.World;

namespace DesktopPet.Core.Tests.World;

public class WallBuilderTests
{
    private static FakeDesktop Screen() => new FakeDesktop().Monitor(0, 0, 1920, 1080, taskbar: 40);

    private static (double X, double Top, double Bottom, WallSide Side)[] Walls(DesktopSnapshot s, string key) =>
        WallBuilder.Build(s).Where(w => w.Key == key).Select(w => (w.X, w.Top, w.Bottom, w.Side)).ToArray();

    [Fact]
    public void Window_has_climbable_outside_faces_on_both_sides()
    {
        var s = Screen().Window(5, 400, 300, 600, 500).Snapshot();

        Assert.Equal([(400.0, 300.0, 800.0, WallSide.PetOnLeft)], Walls(s, WallBuilder.SideKey(5, "L")));
        Assert.Equal([(1000.0, 300.0, 800.0, WallSide.PetOnRight)], Walls(s, WallBuilder.SideKey(5, "R")));
        var left = WallBuilder.Build(s).First(w => w.Key == WallBuilder.SideKey(5, "L"));
        Assert.Equal((WallKind.WindowSide, (long?)5), (left.Kind, left.WindowId));
    }

    [Fact]
    public void Window_in_front_covering_an_edge_splits_it()
    {
        var s = Screen()
            .Window(1, 300, 450, 200, 100) // covers x=400 between y 450 and 550
            .Window(5, 400, 300, 600, 500)
            .Snapshot();

        Assert.Equal(
            [(400.0, 300.0, 450.0, WallSide.PetOnLeft), (400.0, 550.0, 800.0, WallSide.PetOnLeft)],
            Walls(s, WallBuilder.SideKey(5, "L")));
    }

    [Fact]
    public void Side_is_clipped_to_the_work_area_and_dropped_when_off_screen()
    {
        var s = Screen()
            .Window(1, 200, 900, 400, 400)   // runs down behind the taskbar (work area ends at 1040)
            .Window(2, 0, 100, 400, 300)     // left face would be off-screen (column -1)
            .Snapshot();

        Assert.Equal([(200.0, 900.0, 1040.0, WallSide.PetOnLeft)], Walls(s, WallBuilder.SideKey(1, "L")));
        Assert.Empty(Walls(s, WallBuilder.SideKey(2, "L")));
    }

    [Fact]
    public void Maximized_windows_have_no_climbable_sides()
    {
        var s = Screen().Window(1, 0, 0, 1920, 1040, maximized: true).Snapshot();
        Assert.DoesNotContain(WallBuilder.Build(s), w => w.Kind == WallKind.WindowSide);
    }

    [Fact]
    public void Screen_edges_are_walls_but_seams_between_monitors_are_not()
    {
        var s = new FakeDesktop().Monitor(0, 0, 1920, 1080).Monitor(1920, 0, 1920, 1080).Snapshot();
        var edges = WallBuilder.Build(s).Where(w => w.Kind == WallKind.ScreenEdge).Select(w => (w.X, w.Side)).ToArray();

        Assert.Equal([(0.0, WallSide.PetOnRight), (3840.0, WallSide.PetOnLeft)], edges);
    }

    [Fact]
    public void Step_between_monitors_with_different_floors_is_a_short_wall()
    {
        // A's floor 1080; B's taskbar makes its floor 1032. Coming from A, the 48 px rise at x=1920 is a wall.
        var s = new FakeDesktop().Monitor(0, 0, 1920, 1080).Monitor(1920, 0, 1920, 1080, taskbar: 48).Snapshot();
        var step = Assert.Single(WallBuilder.Build(s), w => w.Kind == WallKind.ScreenEdge && w.X == 1920);
        Assert.Equal((1032.0, 1080.0, WallSide.PetOnLeft), (step.Top, step.Bottom, step.Side));
    }
}
