namespace DesktopPet.Core.Animation;

/// <summary>
/// The pet's animation set. Renderers must provide <see cref="AnimationDef.FrameCount"/> frames for each.
/// Art faces right; renderers mirror it when the frame says FacingLeft.
/// </summary>
public static class Animations
{
    /// <summary>Breathing loop with a blink near the end; 2 seconds per cycle.</summary>
    public static readonly AnimationDef Idle = new("idle", frameCount: 16, fps: 8, loop: true);

    /// <summary>Four-step waddle.</summary>
    public static readonly AnimationDef Walk = new("walk", frameCount: 4, fps: 8, loop: true);

    /// <summary>Plopped down, content, slow sway.</summary>
    public static readonly AnimationDef Sit = new("sit", frameCount: 2, fps: 1.5, loop: true);

    /// <summary>Stretched, wide-eyed, feet fluttering.</summary>
    public static readonly AnimationDef Fall = new("fall", frameCount: 2, fps: 10, loop: true);

    /// <summary>Squash on impact, recovering over 4 frames.</summary>
    public static readonly AnimationDef Land = new("land", frameCount: 4, fps: 14, loop: false);

    /// <summary>Dangling from the cursor, feet kicking.</summary>
    public static readonly AnimationDef Held = new("held", frameCount: 2, fps: 6, loop: true);

    /// <summary>Clicked: happy wiggle.</summary>
    public static readonly AnimationDef Happy = new("happy", frameCount: 4, fps: 8, loop: true);

    /// <summary>Hanging by both hands from a title bar, gently swaying.</summary>
    public static readonly AnimationDef Hang = new("hang", frameCount: 2, fps: 1.5, loop: true);

    /// <summary>Moving hand over hand along a title bar.</summary>
    public static readonly AnimationDef Shimmy = new("shimmy", frameCount: 2, fps: 5, loop: true);

    /// <summary>Idle when unhappy: droopy eyes, frown, slow breathing.</summary>
    public static readonly AnimationDef IdleSad = new("idle_sad", frameCount: 2, fps: 1, loop: true);

    /// <summary>Curled up asleep, a "z" drifting up.</summary>
    public static readonly AnimationDef Sleep = new("sleep", frameCount: 2, fps: 1, loop: true);

    /// <summary>Munching a snack.</summary>
    public static readonly AnimationDef Eat = new("eat", frameCount: 2, fps: 4, loop: true);

    /// <summary>Climbing a wall to its right (mirrored for walls on the left), hand over hand.</summary>
    public static readonly AnimationDef Climb = new("climb", frameCount: 2, fps: 4, loop: true);

    /// <summary>Holding still on a wall.</summary>
    public static readonly AnimationDef Cling = new("cling", frameCount: 2, fps: 0.8, loop: true);

    /// <summary>Leaning into a window's side, pushing it along.</summary>
    public static readonly AnimationDef Push = new("push", frameCount: 4, fps: 5, loop: true);

    /// <summary>Sitting on the edge of a title bar, legs dangling over. Drawn sunk by <see cref="LedgeSinkFraction"/>.</summary>
    public static readonly AnimationDef SitLedge = new("sit_ledge", frameCount: 2, fps: 1.5, loop: true);

    /// <summary>
    /// For <see cref="SitLedge"/>: the sprite is drawn this fraction of its height lower than the feet point, so the
    /// body rests on the edge and the legs hang below it.
    /// </summary>
    public const double LedgeSinkFraction = 9.0 / 32.0;

    public static IReadOnlyList<AnimationDef> All { get; } =
        [Idle, Walk, Sit, Fall, Land, Held, Happy, Hang, Shimmy, IdleSad, Sleep, Eat, Climb, Cling, Push, SitLedge];
}
