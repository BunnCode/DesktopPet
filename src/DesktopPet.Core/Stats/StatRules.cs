namespace DesktopPet.Core.Stats;

/// <summary>Tamagotchi tuning. Rates are points per real hour.</summary>
public sealed record StatRules
{
    public static StatRules Default { get; } = new();

    public double HungerPerHour { get; init; } = -8;
    public double HappinessPerHour { get; init; } = -5;
    public double EnergyPerHour { get; init; } = -6;

    /// <summary>Extra energy drain while active (walking, playing).</summary>
    public double ActiveEnergyPerHour { get; init; } = -6;

    /// <summary>Energy gained per hour asleep (replaces the drain).</summary>
    public double SleepEnergyPerHour { get; init; } = 45;

    /// <summary>Hunger and happiness decay this much slower while asleep.</summary>
    public double SleepDecayFactor { get; init; } = 0.5;

    /// <summary>Below this a stat is critical (shown with a warning, pet visibly suffers).</summary>
    public double Critical { get; init; } = 15;

    /// <summary>
    /// While the user is away (screen locked, PC asleep or off, app closed) no stat may drop below this,
    /// which sits just above <see cref="Critical"/>. A stat already below it doesn't drop further.
    /// </summary>
    public double AwayFloor { get; init; } = 20;

    /// <summary>A gap this long between stat updates means the process was frozen (PC asleep): counted as away.</summary>
    public TimeSpan AwayGap { get; init; } = TimeSpan.FromMinutes(1);

    // ---- Actions ----
    public double FeedHunger { get; init; } = 35;
    public double FeedHappiness { get; init; } = 3;
    public double PlayHappiness { get; init; } = 22;
    public double PlayEnergy { get; init; } = -8;
    public double PlayHunger { get; init; } = -4;
    public double PetHappiness { get; init; } = 3;

    /// <summary>Sleep ends by itself once energy is back up here.</summary>
    public double WellRested { get; init; } = 97;

    /// <summary>Below this happiness the pet mopes (sad idle, sits more, walks less).</summary>
    public double Sad { get; init; } = 30;

    /// <summary>Below this energy the pet starts wanting to sleep and slows down.</summary>
    public double Tired { get; init; } = 50;
}
