using Deskling.Core.Math;
using Deskling.Core.World;

namespace Deskling.Core.Tests.World;

public class WindowMoveGuardTests
{
    private static readonly RectI Work = new(0, 0, 1920, 1040);

    [Fact]
    public void Moves_freely_inside_the_work_area()
    {
        Assert.Equal((10, -5), WindowMoveGuard.Clamp(RectI.FromSize(500, 300, 400, 300), Work, 10, -5));
    }

    [Theory]
    [InlineData(1500, 10, 10)]   // right edge at 1900: 20 px of room, 10 requested
    [InlineData(1510, 50, 10)]   // only 10 px of room left
    [InlineData(1520, 50, 0)]    // flush with the edge: can't go further
    public void Stops_at_the_right_edge(int left, int dx, int expected)
    {
        Assert.Equal((expected, 0), WindowMoveGuard.Clamp(RectI.FromSize(left, 300, 400, 300), Work, dx, 0));
    }

    [Fact]
    public void Stops_at_the_left_top_and_bottom_edges()
    {
        Assert.Equal((-3, 0), WindowMoveGuard.Clamp(RectI.FromSize(3, 300, 400, 300), Work, -50, 0));
        Assert.Equal((0, -7), WindowMoveGuard.Clamp(RectI.FromSize(500, 7, 400, 300), Work, 0, -50));
        Assert.Equal((0, 0), WindowMoveGuard.Clamp(RectI.FromSize(500, 740, 400, 300), Work, 0, 50));
    }

    [Fact]
    public void A_window_already_partly_off_screen_can_come_back_but_not_go_further_out()
    {
        var hanging = RectI.FromSize(1700, 300, 400, 300); // right edge at 2100, past 1920
        Assert.Equal((0, 0), WindowMoveGuard.Clamp(hanging, Work, 30, 0));
        Assert.Equal((-30, 0), WindowMoveGuard.Clamp(hanging, Work, -30, 0));
    }

    [Fact]
    public void A_window_wider_than_the_screen_cannot_be_moved_sideways()
    {
        var huge = RectI.FromSize(-10, 100, 2000, 500);
        Assert.Equal((0, 0), WindowMoveGuard.Clamp(huge, Work, 20, 0));
        Assert.Equal((0, 0), WindowMoveGuard.Clamp(huge, Work, -20, 0));
    }

    [Fact]
    public void Repeated_pushes_never_take_a_window_off_screen()
    {
        var frame = RectI.FromSize(1000, 300, 400, 300);
        for (var i = 0; i < 1000; i++)
        {
            var (dx, _) = WindowMoveGuard.Clamp(frame, Work, 7, 0);
            frame = frame.Offset(dx, 0);
            Assert.True(frame.Right <= Work.Right);
        }

        Assert.Equal(Work.Right, frame.Right);
    }
}
