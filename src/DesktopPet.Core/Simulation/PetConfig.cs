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

    /// <summary>How long the happy reaction to a click lasts.</summary>
    public DurationRange HappySeconds { get; init; } = new(1.0, 1.5);

    /// <summary>Chance that a new walk heads the other way.</summary>
    public double WalkTurnChance { get; init; } = 0.5;
}

/// <summary>Relative likelihood of each voluntary behavior when the current one ends.</summary>
public sealed record BehaviorWeights(double Idle = 3, double Walk = 5, double Sit = 1.5);

public readonly record struct DurationRange(double Min, double Max);
