namespace DesktopPet.Core.Simulation;

/// <summary>
/// Tuning knobs. Distances and speeds are in DIPs and get multiplied by the scale of the monitor the pet is on,
/// so motion looks the same on every display. Will grow per milestone and can later be loaded from JSON.
/// </summary>
public sealed record PetConfig
{
    public static PetConfig Default { get; } = new();

    /// <summary>Pet size in device-independent pixels; physical size = this × monitor scale.</summary>
    public double SizeDip { get; init; } = 64;

    /// <summary>Half the visible body width as a fraction of <see cref="SizeDip"/>. Used for wall contact (the sprite has transparent margins).</summary>
    public double BodyHalfWidthFraction { get; init; } = 0.42;

    public double StepSeconds { get; init; } = 1.0 / 60.0;

    public int MaxStepsPerFrame { get; init; } = 5;

    // ---- Physics ----

    /// <summary>Downward acceleration, DIP/s². A 1000 DIP drop takes about 0.8 s.</summary>
    public double GravityDip { get; init; } = 3000;

    /// <summary>Maximum fall speed, DIP/s.</summary>
    public double TerminalVelocityDip { get; init; } = 2200;

    /// <summary>Exponential decay rate of horizontal speed in the air, per second.</summary>
    public double AirDragPerSecond { get; init; } = 0.4;

    /// <summary>Fraction of horizontal speed kept when bouncing off a wall.</summary>
    public double WallRestitution { get; init; } = 0.55;

    /// <summary>Deceleration of a skid after landing with horizontal speed, DIP/s².</summary>
    public double GroundFrictionDip { get; init; } = 2400;

    /// <summary>How long the pet may be somewhere unrecoverable (see <see cref="World.WorldQuery.IsLost"/>) before respawning.</summary>
    public double LostGraceSeconds { get; init; } = 0.5;

    // ---- Mouse ----

    /// <summary>Pointer travel before a press becomes a drag; shorter presses are clicks ("pet the pet").</summary>
    public double DragThresholdDip { get; init; } = 4;

    /// <summary>How much recent pointer motion the throw velocity is fitted over.</summary>
    public double ThrowWindowSeconds { get; init; } = 0.1;

    /// <summary>Throw speed cap, DIP/s.</summary>
    public double MaxThrowSpeedDip { get; init; } = 3500;

    // ---- Behavior ----

    /// <summary>Walking speed, DIP/s.</summary>
    public double WalkSpeedDip { get; init; } = 60;

    public BehaviorWeights Weights { get; init; } = new();

    public DurationRange IdleSeconds { get; init; } = new(2, 5);
    public DurationRange WalkSeconds { get; init; } = new(2, 7);
    public DurationRange SitSeconds { get; init; } = new(4, 10);

    /// <summary>A nap chosen by the pet itself; tucking it in from the menu sleeps until rested instead.</summary>
    public DurationRange NapSeconds { get; init; } = new(30, 120);

    public double EatSeconds { get; init; } = 2.5;

    /// <summary>Hops when playing, and how high each one is (DIP/s of upward launch speed).</summary>
    public int PlayHops { get; init; } = 3;

    public double PlayHopSpeedDip { get; init; } = 650;

    /// <summary>Tamagotchi rates and thresholds.</summary>
    public Stats.StatRules Stats { get; init; } = Core.Stats.StatRules.Default;

    /// <summary>How long the happy reaction to a click lasts.</summary>
    public DurationRange HappySeconds { get; init; } = new(1.0, 1.5);

    /// <summary>Chance that a new walk heads the other way.</summary>
    public double WalkTurnChance { get; init; } = 0.5;

    // ---- New pets ----

    /// <summary>
    /// A new pet's neediness multipliers (how fast hunger, happiness and energy drop) are rolled in this range,
    /// log-uniformly: halving is as likely as doubling.
    /// </summary>
    public Traits.TraitRange NewPetNeediness { get; init; } = new(0.5, 2.0);

    /// <summary>A new pet's behavior multipliers (idle, walk, sit, sleep, climb, push) are rolled in this range.</summary>
    public Traits.TraitRange NewPetBehavior { get; init; } = new(0.5, 2.0);

    // ---- Walls: climbing window sides and screen edges, pushing windows ----

    public bool EnableClimbing { get; init; } = true;

    /// <summary>Lets the pet push real windows around. Moves the user's windows, so it can be switched off.</summary>
    public bool EnablePushing { get; init; } = true;

    /// <summary>Walking into a wall: chance to start climbing it...</summary>
    public double ClimbChance { get; init; } = 0.35;

    /// <summary>...or (window sides only) to push the window. Otherwise it turns at screen edges and walks past windows.</summary>
    public double PushChance { get; init; } = 0.25;

    /// <summary>Walking off the end of a window top: chance to climb down that window's side instead of dropping.</summary>
    public double ClimbDownChance { get; init; } = 0.4;

    /// <summary>Climbing speed, DIP/s.</summary>
    public double ClimbSpeedDip { get; init; } = 40;

    /// <summary>Thrown into a wall slower than this (DIP/s, horizontal), it grabs on.</summary>
    public double ClingMaxSpeedDip { get; init; } = 2500;

    /// <summary>Released by the mouse with its side this close to a wall it's facing (DIP), it grabs on.</summary>
    public double ClingReachDip { get; init; } = 16;

    public DurationRange ClimbSeconds { get; init; } = new(2, 6);
    public DurationRange ClingSeconds { get; init; } = new(2, 5);
    public DurationRange PushSeconds { get; init; } = new(2, 5);

    /// <summary>Pushing speed, DIP/s (the window moves this fast).</summary>
    public double PushSpeedDip { get; init; } = 30;

    // ---- Hanging from title bars ----

    /// <summary>
    /// Whether the pet can hang from the title bars of maximized / top-pinned windows. Off for now: with it off,
    /// no hang bars are built, so nothing can grab, catch or spawn onto one.
    /// </summary>
    public bool EnableHanging { get; init; }

    /// <summary>Assumed title-bar height for hang bars, DIP.</summary>
    public double CaptionHeightDip { get; init; } = World.PlatformBuilder.DefaultCaptionHeightDip;

    /// <summary>Released by the mouse with the top of the pet this close to a hang bar (DIP), it grabs on.</summary>
    public double HangReachDip { get; init; } = 24;

    /// <summary>...unless it was thrown faster than this (DIP/s).</summary>
    public double HangCatchMaxSpeedDip { get; init; } = 1500;

    public DurationRange HangSeconds { get; init; } = new(3, 8);
    public DurationRange ShimmySeconds { get; init; } = new(2, 5);

    /// <summary>Hand-over-hand speed along a hang bar, DIP/s.</summary>
    public double ShimmySpeedDip { get; init; } = 35;

    /// <summary>When a hang ends: chance to shimmy along the bar...</summary>
    public double HangShimmyChance { get; init; } = 0.35;

    /// <summary>...and chance to let go and drop. The rest of the time it keeps hanging.</summary>
    public double HangDropChance { get; init; } = 0.3;

    // ---- Furniture ----

    /// <summary>When pets go and use furniture on their own, and how it moves them.</summary>
    public FurnitureRules Furniture { get; init; } = new();
}

/// <summary>Tuning for furniture: when pets choose it, how they get there, and the kick and bounce physics.</summary>
public sealed record FurnitureRules
{
    /// <summary>A need below this makes furniture that meets it attractive, more so the lower it is.</summary>
    public double NeedBelow { get; init; } = 75;

    /// <summary>Weight of "go use furniture" when a need it meets is empty (scaled down as the need is fuller).</summary>
    public double Weight { get; init; } = 4;

    /// <summary>Weight of trying out fun furniture (happiness or no need) even when nothing is low.</summary>
    public double CuriosityWeight { get; init; } = 0.4;

    /// <summary>A trip to furniture that takes longer than this is abandoned.</summary>
    public double ErrandSeconds { get; init; } = 60;

    /// <summary>Furniture a pet couldn't reach isn't tried again for this long.</summary>
    public double RetrySeconds { get; init; } = 30;

    /// <summary>How hard a pet kicks a ball: sideways and upward speed, DIP/s.</summary>
    public double KickSpeedDip { get; init; } = 380;

    public double KickLiftDip { get; init; } = 420;

    /// <summary>Hops on furniture with the Hop effect (the couch), DIP/s of upward speed.</summary>
    public double HopSpeedDip { get; init; } = 450;

    /// <summary>Trampoline: this many hops, each this fast, then a launch this fast (about 900 DIP high).</summary>
    public int BounceHops { get; init; } = 3;

    public double BounceHopSpeedDip { get; init; } = 750;

    public double BounceLaunchDip { get; init; } = 2300;
}

/// <summary>
/// Base likelihood of each voluntary behavior when the current one ends; <see cref="Behavior.BehaviorPicker"/>
/// adjusts them by stats (tired → sleep, sad → sit). Sleep's weight only applies once tired.
/// </summary>
public sealed record BehaviorWeights(double Idle = 3, double Walk = 5, double Sit = 1.5, double Sleep = 6);

public readonly record struct DurationRange(double Min, double Max);
