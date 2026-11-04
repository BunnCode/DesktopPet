using DesktopPet.Core.Simulation;

namespace DesktopPet.Core.Traits;

/// <summary>
/// What makes one pet different from another: its name, how fast its needs drop and how it likes to spend its time.
/// Multipliers are 1 for an average pet. Kept in the pet's own <c>traits.json</c> and applied on top of the shared
/// settings with <see cref="ApplyTo"/>.
/// </summary>
public sealed record PetTraits
{
    public string Name { get; init; } = string.Empty;

    public Neediness Neediness { get; init; } = new();

    public BehaviorTraits Behavior { get; init; } = new();

    /// <summary>An average pet: every multiplier 1.</summary>
    public static PetTraits Neutral(string name) => new() { Name = name };

    /// <summary>
    /// The settings this pet actually runs on: the shared ones with its stat decay rates and behavior odds scaled.
    /// Climb and push chances are scaled down together if they'd add up to more than 1.
    /// </summary>
    public PetConfig ApplyTo(PetConfig shared)
    {
        ArgumentNullException.ThrowIfNull(shared);
        var n = Neediness;
        var b = Behavior;
        var s = shared.Stats;
        var w = shared.Weights;

        var climb = shared.ClimbChance * b.Climb;
        var push = shared.PushChance * b.Push;
        if (climb + push > 1)
        {
            var total = climb + push;
            climb /= total;
            push /= total;
        }

        return shared with
        {
            Stats = s with
            {
                HungerPerHour = s.HungerPerHour * n.Hunger,
                HappinessPerHour = s.HappinessPerHour * n.Happiness,
                EnergyPerHour = s.EnergyPerHour * n.Energy,
                ActiveEnergyPerHour = s.ActiveEnergyPerHour * n.Energy,
            },
            Weights = new BehaviorWeights(w.Idle * b.Idle, w.Walk * b.Walk, w.Sit * b.Sit, w.Sleep * b.Sleep),
            ClimbChance = climb,
            PushChance = push,
        };
    }
}

/// <summary>How fast each need drops compared with an average pet (2 = twice as fast; sleep still restores energy at the normal rate).</summary>
public sealed record Neediness(double Hunger = 1, double Happiness = 1, double Energy = 1);

/// <summary>How often the pet chooses each behavior compared with an average pet (2 = twice as likely).</summary>
public sealed record BehaviorTraits(double Idle = 1, double Walk = 1, double Sit = 1, double Sleep = 1, double Climb = 1, double Push = 1);

/// <summary>A range for rolling a new pet's multipliers.</summary>
public readonly record struct TraitRange(double Min, double Max);
