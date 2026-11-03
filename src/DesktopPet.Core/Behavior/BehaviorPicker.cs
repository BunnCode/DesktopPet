using DesktopPet.Core.Abstractions;
using DesktopPet.Core.Simulation;

namespace DesktopPet.Core.Behavior;

/// <summary>Chooses the next voluntary behavior by weighted random. M6 will modulate the weights by stats.</summary>
internal static class BehaviorPicker
{
    public static PetStateId Pick(PetConfig config, IRandom random)
    {
        var w = config.Weights;
        var roll = random.NextDouble() * (w.Idle + w.Walk + w.Sit);

        if (roll < w.Idle)
        {
            return PetStateId.Idle;
        }

        roll -= w.Idle;
        return roll < w.Walk ? PetStateId.Walk : PetStateId.Sit;
    }
}
