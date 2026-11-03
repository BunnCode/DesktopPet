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

    public double StepSeconds { get; init; } = 1.0 / 60.0;

    public int MaxStepsPerFrame { get; init; } = 5;

    /// <summary>Downward acceleration, DIP/s². A 1000 DIP drop takes about 0.8 s.</summary>
    public double GravityDip { get; init; } = 3000;

    /// <summary>Maximum fall speed, DIP/s.</summary>
    public double TerminalVelocityDip { get; init; } = 2200;

    /// <summary>How long the pet may be somewhere unrecoverable (see <see cref="World.WorldQuery.IsLost"/>) before respawning.</summary>
    public double LostGraceSeconds { get; init; } = 0.5;
}
