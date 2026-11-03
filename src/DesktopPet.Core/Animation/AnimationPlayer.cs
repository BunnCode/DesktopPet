namespace DesktopPet.Core.Animation;

/// <summary>Advances an <see cref="AnimationDef"/> over simulation time and reports the current frame.</summary>
public sealed class AnimationPlayer
{
    private double _time;

    public AnimationPlayer(AnimationDef initial)
    {
        ArgumentNullException.ThrowIfNull(initial);
        Current = initial;
    }

    public AnimationDef Current { get; private set; }
    public int FrameIndex { get; private set; }

    /// <summary>True once a non-looping animation has shown its last frame for its full duration.</summary>
    public bool IsFinished { get; private set; }

    /// <summary>Switches animation. Playing the one already running is a no-op unless <paramref name="restart"/> is set.</summary>
    public void Play(AnimationDef animation, bool restart = false)
    {
        ArgumentNullException.ThrowIfNull(animation);
        if (!restart && animation == Current)
        {
            return;
        }

        Current = animation;
        _time = 0;
        FrameIndex = 0;
        IsFinished = false;
    }

    public void Update(double dt)
    {
        if (!(dt > 0) || IsFinished)
        {
            return;
        }

        _time += dt;
        var duration = Current.Duration;

        if (Current.Loop)
        {
            _time %= duration; // keeps precision stable over long sessions
        }
        else if (_time >= duration)
        {
            _time = duration;
            FrameIndex = Current.FrameCount - 1;
            IsFinished = true;
            return;
        }

        FrameIndex = System.Math.Min((int)(_time * Current.Fps), Current.FrameCount - 1);
    }
}
