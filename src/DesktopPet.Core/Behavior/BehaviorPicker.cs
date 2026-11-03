using DesktopPet.Core.Abstractions;
using DesktopPet.Core.Simulation;
using DesktopPet.Core.Stats;

namespace DesktopPet.Core.Behavior;

/// <summary>
/// Chooses the next voluntary behavior by weighted random, shaped by needs:
/// tired → sleep becomes likely and walking rarer; sad → sits and mopes more, walks less.
/// With healthy stats the base weights apply unchanged.
/// </summary>
internal static class BehaviorPicker
{
    public static PetStateId Pick(PetConfig config, IRandom random, PetStats stats)
    {
        var (idle, walk, sit, sleep) = Weights(config, stats);
        var roll = random.NextDouble() * (idle + walk + sit + sleep);

        if ((roll -= idle) < 0)
        {
            return PetStateId.Idle;
        }

        if ((roll -= walk) < 0)
        {
            return PetStateId.Walk;
        }

        return roll - sit < 0 ? PetStateId.Sit : PetStateId.Sleep;
    }

    /// <summary>Effective weights for the given stats (exposed for tests and tuning).</summary>
    internal static (double Idle, double Walk, double Sit, double Sleep) Weights(PetConfig config, PetStats stats)
    {
        var w = config.Weights;
        var rules = config.Stats;

        // Tiredness: 0 when energy ≥ Tired, rising to 1 at zero energy.
        var tiredness = stats.Energy >= rules.Tired ? 0 : 1 - (stats.Energy / rules.Tired);
        var sad = stats.Happiness < rules.Sad;

        var walk = w.Walk * (1 - (0.8 * tiredness)) * (sad ? 0.5 : 1);
        var sit = w.Sit + (sad ? 2 : 0);
        var sleep = w.Sleep * tiredness * tiredness;
        return (w.Idle, walk, sit, sleep);
    }
}
