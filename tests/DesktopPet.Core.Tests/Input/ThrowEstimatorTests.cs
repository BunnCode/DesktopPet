using DesktopPet.Core.Input;
using DesktopPet.Core.Math;

namespace DesktopPet.Core.Tests.Input;

public class ThrowEstimatorTests
{
    [Fact]
    public void Constant_velocity_is_recovered_exactly()
    {
        var e = new ThrowEstimator(0.1);
        for (var i = 0; i <= 10; i++)
        {
            var t = i / 60.0;
            e.Add(t, new Vec2(100 + (900 * t), 500 - (300 * t)));
        }

        var v = e.Velocity(10 / 60.0);
        Assert.Equal(900, v.X, 3);
        Assert.Equal(-300, v.Y, 3);
    }

    [Fact]
    public void Jitter_is_smoothed()
    {
        var e = new ThrowEstimator(0.1);
        for (var i = 0; i <= 6; i++)
        {
            var t = i / 60.0;
            var jitter = i % 2 == 0 ? 2 : -2;
            e.Add(t, new Vec2((1000 * t) + jitter, 0));
        }

        Assert.InRange(e.Velocity(6 / 60.0).X, 850, 1150); // naive last-delta would be ~1000 ± 240
    }

    [Fact]
    public void Old_motion_does_not_count_after_holding_still()
    {
        var e = new ThrowEstimator(0.1);
        for (var i = 0; i <= 10; i++)
        {
            e.Add(i / 60.0, new Vec2(i * 30, 0)); // fast move...
        }

        for (var i = 11; i <= 40; i++)
        {
            e.Add(i / 60.0, new Vec2(300, 0)); // ...then half a second still
        }

        Assert.Equal(Vec2.Zero, e.Velocity(40 / 60.0));
    }

    [Fact]
    public void Too_few_samples_give_zero()
    {
        var e = new ThrowEstimator(0.1);
        Assert.Equal(Vec2.Zero, e.Velocity(0));
        e.Add(0, new Vec2(5, 5));
        Assert.Equal(Vec2.Zero, e.Velocity(0));
        e.Add(0, new Vec2(9, 9)); // same timestamp: slope undefined
        Assert.Equal(Vec2.Zero, e.Velocity(0));
    }

    [Fact]
    public void Clear_forgets_previous_gesture()
    {
        var e = new ThrowEstimator(0.1);
        e.Add(0, Vec2.Zero);
        e.Add(0.05, new Vec2(100, 0));
        e.Clear();
        e.Add(0.06, new Vec2(100, 0));
        Assert.Equal(Vec2.Zero, e.Velocity(0.06));
    }
}
