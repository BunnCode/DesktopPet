using System.Text.Json.Serialization;
using Deskling.Core.Chat;
using Deskling.Core.Simulation;

namespace Deskling.Core.Traits;

/// <summary>
/// What makes one pet different from another: its name, how fast its needs drop and how it likes to spend its time.
/// Multipliers are 1 for an average pet. Kept in the <c>Traits</c> section of the pet's <c>save.json</c> and applied on top of the shared
/// settings with <see cref="ApplyTo"/>.
/// </summary>
public sealed record PetTraits
{
    public string Name { get; init; } = string.Empty;

    public Neediness Neediness { get; init; } = new();

    public BehaviorTraits Behavior { get; init; } = new();

    /// <summary>How often it says each kind of line (used by <see cref="Chat.ChatDirector"/>, not by <see cref="ApplyTo"/>).</summary>
    public ChatTraits Chat { get; init; } = new();

    /// <summary>Fixed (neutered): it never mates. Set from the menu or by hand.</summary>
    public bool Fixed { get; init; }

    /// <summary>
    /// Which built-in character it is (an id from <see cref="Characters.All"/>, such as "emberdonkey"): the art drawn
    /// where its sprites folders have none, and what it is in its personality and chat. Null: the default character
    /// that goes with the pet's number (<see cref="Characters.For"/>).
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Character { get; init; }

    /// <summary>
    /// The built-in rabbit's jacket colour, #RRGGBB. Null: the colour that goes with the pet's number. Hatched pets get
    /// a blend of their parents'.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Jacket { get; init; }

    /// <summary>The names of its parents, for a pet that hatched from an egg; null otherwise.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public TextList? Parents { get; init; }

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

/// <summary>
/// How often the pet chooses each behavior compared with an average pet (2 = twice as likely). <see cref="Mating"/>
/// scales its chance of mating (used by <see cref="Breeding.BreedingDirector"/>, not by <see cref="PetTraits.ApplyTo"/>).
/// </summary>
public sealed record BehaviorTraits(
    double Idle = 1, double Walk = 1, double Sit = 1, double Sleep = 1, double Climb = 1, double Push = 1, double Mating = 1);

/// <summary>
/// How often the pet says each kind of line compared with an average pet (0 = never, 2 = twice as often).
/// <see cref="Curiosity"/> is how often it talks or writes about what it reads on screen.
/// </summary>
public sealed record ChatTraits(double Needy = 1, double Banter = 1, double Jokes = 1, double Notes = 1, double Curiosity = 1);

/// <summary>A range for rolling a new pet's multipliers.</summary>
public readonly record struct TraitRange(double Min, double Max);
