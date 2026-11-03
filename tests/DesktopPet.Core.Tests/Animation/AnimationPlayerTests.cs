using DesktopPet.Core.Animation;

namespace DesktopPet.Core.Tests.Animation;

public class AnimationPlayerTests
{
    private static readonly AnimationDef Loop4 = new("loop", frameCount: 4, fps: 10, loop: true);   // 0.4 s
    private static readonly AnimationDef Once3 = new("once", frameCount: 3, fps: 10, loop: false);  // 0.3 s

    [Fact]
    public void Advances_one_frame_per_frame_period()
    {
        var p = new AnimationPlayer(Loop4);
        Assert.Equal(0, p.FrameIndex);
        p.Update(0.05);
        Assert.Equal(0, p.FrameIndex);
        p.Update(0.06);
        Assert.Equal(1, p.FrameIndex);
        p.Update(0.1);
        Assert.Equal(2, p.FrameIndex);
    }

    [Fact]
    public void Loop_wraps_around()
    {
        var p = new AnimationPlayer(Loop4);
        p.Update(0.45);
        Assert.Equal(0, p.FrameIndex);
        Assert.False(p.IsFinished);
    }

    [Fact]
    public void Loop_stays_in_range_over_many_small_steps()
    {
        var p = new AnimationPlayer(Loop4);
        for (var i = 0; i < 100_000; i++)
        {
            p.Update(1.0 / 60.0);
            Assert.InRange(p.FrameIndex, 0, 3);
        }
    }

    [Fact]
    public void Non_loop_holds_last_frame_and_finishes()
    {
        var p = new AnimationPlayer(Once3);
        p.Update(0.25);
        Assert.Equal(2, p.FrameIndex);
        Assert.False(p.IsFinished);
        p.Update(1.0);
        Assert.Equal(2, p.FrameIndex);
        Assert.True(p.IsFinished);
    }

    [Fact]
    public void Play_same_animation_does_not_restart_unless_asked()
    {
        var p = new AnimationPlayer(Loop4);
        p.Update(0.25);
        p.Play(Loop4);
        Assert.Equal(2, p.FrameIndex);
        p.Play(Loop4, restart: true);
        Assert.Equal(0, p.FrameIndex);
    }

    [Fact]
    public void Play_other_animation_resets_state()
    {
        var p = new AnimationPlayer(Once3);
        p.Update(1.0);
        Assert.True(p.IsFinished);
        p.Play(Loop4);
        Assert.Same(Loop4, p.Current);
        Assert.Equal(0, p.FrameIndex);
        Assert.False(p.IsFinished);
    }

    [Fact]
    public void Definition_validates_arguments()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new AnimationDef("x", 0, 10, true));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AnimationDef("x", 1, 0, true));
        Assert.Throws<ArgumentException>(() => new AnimationDef("", 1, 10, true));
    }
}
