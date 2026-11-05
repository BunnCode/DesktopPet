using Deskling.Core.Abstractions;
using Deskling.Core.Simulation;
using Deskling.Core.Stats;

namespace Deskling.Core.Behavior;

/// <summary>
/// Chooses the next voluntary behavior by weighted random, shaped by needs:
/// tired → sleep becomes likely and walking rarer; sad → sits and mopes more, walks less.
/// With healthy stats the base weights apply unchanged. Furniture is weighed in by <see cref="FurnitureChooser"/>.
/// </summary>
internal static class BehaviorPicker
{
    /// <summary>
    /// Picks for a pet, including "go use furniture" (weighted by how low the needs it meets are). If furniture is
    /// rolled but none can be reached, picks again without it.
    /// </summary>
    public static PetStateId Pick(PetContext context)
    {
        var furniture = FurnitureChooser.Weight(context);
        if (furniture > 0)
        {
            var (idle, walk, sit, sleep) = Weights(context.Config, context.Stats);
            var total = idle + walk + sit + sleep + furniture;
            if (context.Random.NextDouble() * total >= total - furniture && FurnitureChooser.TryStart(context))
            {
                return PetStateId.GoToFurniture;
            }
        }

        return Pick(context.Config, context.Random, context.Stats, penned: context.Pen is not null);
    }

    /// <param name="config">The tuning (picker weights and stat thresholds).</param>
    /// <param name="random">The source of the roll.</param>
    /// <param name="stats">The pet's needs, which shift the weights.</param>
    /// <param name="penned">Shut in a play pen: it mostly sits (<see cref="FurnitureRules.PenSitWeight"/>).</param>
    public static PetStateId Pick(PetConfig config, IRandom random, PetStats stats, bool penned = false)
    {
        var (idle, walk, sit, sleep) = Weights(config, stats);
        if (penned)
        {
            sit = System.Math.Max(sit, config.Furniture.PenSitWeight);
        }

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
