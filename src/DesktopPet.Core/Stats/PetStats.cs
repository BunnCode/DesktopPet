namespace DesktopPet.Core.Stats;

/// <summary>
/// Tamagotchi needs, each 0–100 where <b>100 is good</b>: <see cref="Hunger"/> 100 = well fed, 0 = starving;
/// <see cref="Happiness"/> 100 = delighted; <see cref="Energy"/> 100 = fully rested.
/// </summary>
public readonly record struct PetStats(double Hunger, double Happiness, double Energy)
{
    public const double Max = 100;

    public static PetStats Fresh { get; } = new(80, 80, 80);

    public PetStats Clamped() => new(Clamp(Hunger), Clamp(Happiness), Clamp(Energy));

    public double Lowest => System.Math.Min(Hunger, System.Math.Min(Happiness, Energy));

    private static double Clamp(double v) => double.IsNaN(v) ? 0 : System.Math.Clamp(v, 0, Max);
}

/// <summary>What the pet is doing, for stat rates.</summary>
public enum StatActivity
{
    /// <summary>Idle, sitting, hanging...</summary>
    Resting,

    /// <summary>Walking, playing, being thrown around.</summary>
    Active,

    Sleeping,
}

/// <summary>Things the user can do to the pet (menu actions).</summary>
public enum PetAction
{
    Feed,
    Play,
    Sleep,
    WakeUp,
}
