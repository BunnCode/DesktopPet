using DesktopPet.Core.Math;

namespace DesktopPet.Core.Tests.Math;

public class MathTests
{
    [Fact]
    public void FromSize_computes_edges()
    {
        var r = RectI.FromSize(10, 20, 100, 50);
        Assert.Equal(new RectI(10, 20, 110, 70), r);
        Assert.Equal(100, r.Width);
        Assert.Equal(50, r.Height);
    }

    [Fact]
    public void Contains_is_half_open()
    {
        var r = new RectI(0, 0, 10, 10);
        Assert.True(r.Contains(new Vec2(0, 0)));
        Assert.True(r.Contains(new Vec2(9.99, 9.99)));
        Assert.False(r.Contains(new Vec2(10, 5)));
        Assert.False(r.Contains(new Vec2(5, 10)));
    }

    [Fact]
    public void Touching_rects_do_not_intersect()
    {
        var a = new RectI(0, 0, 10, 10);
        var b = new RectI(10, 0, 20, 10);
        Assert.False(a.Intersects(b));
        Assert.True(a.Intersect(b).IsEmpty);
    }

    [Fact]
    public void Intersect_returns_overlap()
    {
        var a = new RectI(0, 0, 10, 10);
        var b = new RectI(5, -5, 15, 5);
        Assert.True(a.Intersects(b));
        Assert.Equal(new RectI(5, 0, 10, 5), a.Intersect(b));
    }

    [Fact]
    public void Vec2_round_and_lerp()
    {
        Assert.Equal(new Vec2I(2, -3), new Vec2(1.6, -2.6).Round());
        Assert.Equal(new Vec2(5, 10), Vec2.Lerp(Vec2.Zero, new Vec2(10, 20), 0.5));
    }
}
