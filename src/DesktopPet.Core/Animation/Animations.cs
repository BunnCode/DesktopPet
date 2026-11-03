namespace DesktopPet.Core.Animation;

/// <summary>The pet's animation set. Renderers must provide <see cref="AnimationDef.FrameCount"/> frames for each.</summary>
public static class Animations
{
    /// <summary>Breathing loop with a blink near the end; 2 seconds per cycle.</summary>
    public static readonly AnimationDef Idle = new("idle", frameCount: 16, fps: 8, loop: true);

    public static IReadOnlyList<AnimationDef> All { get; } = [Idle];
}
