namespace DesktopPet.Core.Animation;

/// <summary>The pet's animation set. Renderers must provide <see cref="AnimationDef.FrameCount"/> frames for each.</summary>
public static class Animations
{
    /// <summary>Breathing loop with a blink near the end; 2 seconds per cycle.</summary>
    public static readonly AnimationDef Idle = new("idle", frameCount: 16, fps: 8, loop: true);

    /// <summary>Stretched, wide-eyed, feet fluttering.</summary>
    public static readonly AnimationDef Fall = new("fall", frameCount: 2, fps: 10, loop: true);

    /// <summary>Squash on impact, recovering over 4 frames.</summary>
    public static readonly AnimationDef Land = new("land", frameCount: 4, fps: 14, loop: false);

    public static IReadOnlyList<AnimationDef> All { get; } = [Idle, Fall, Land];
}
