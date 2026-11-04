using DesktopPet.Core.Abstractions;
using DesktopPet.Core.Simulation;

namespace DesktopPet.Core.Traits;

/// <summary>Makes up a new pet: a free name and random multipliers in the ranges from the settings.</summary>
public static class TraitRoller
{
    /// <param name="config">Settings with the ranges to roll in.</param>
    /// <param name="random">Randomness.</param>
    /// <param name="takenNames">Names of the pets that already exist; the new name is none of them.</param>
    public static PetTraits Roll(PetConfig config, IRandom random, IEnumerable<string> takenNames)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(random);
        var need = config.NewPetNeediness;
        var act = config.NewPetBehavior;

        return new PetTraits
        {
            Name = PetNames.Pick(random, takenNames),
            Neediness = new Neediness(Multiplier(random, need), Multiplier(random, need), Multiplier(random, need)),
            Behavior = new BehaviorTraits(
                Multiplier(random, act), Multiplier(random, act), Multiplier(random, act),
                Multiplier(random, act), Multiplier(random, act), Multiplier(random, act)),
        };
    }

    /// <summary>
    /// Log-uniform in the range, rounded to 2 decimals: with 0.5–2, halving is as likely as doubling and the median is 1.
    /// </summary>
    internal static double Multiplier(IRandom random, TraitRange range)
    {
        if (range.Min <= 0 || range.Max <= range.Min)
        {
            return System.Math.Round(System.Math.Max(range.Min, 0), 2);
        }

        var log = random.Range(System.Math.Log(range.Min), System.Math.Log(range.Max));
        return System.Math.Round(System.Math.Exp(log), 2);
    }
}
