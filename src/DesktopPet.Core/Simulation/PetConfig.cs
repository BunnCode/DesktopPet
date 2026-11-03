namespace DesktopPet.Core.Simulation;

/// <summary>Tuning knobs. Will grow per milestone and can later be loaded from JSON.</summary>
public sealed record PetConfig
{
    public static PetConfig Default { get; } = new();

    /// <summary>Pet size in device-independent pixels; physical size = this × monitor scale.</summary>
    public double SizeDip { get; init; } = 64;

    public double StepSeconds { get; init; } = 1.0 / 60.0;

    public int MaxStepsPerFrame { get; init; } = 5;
}
