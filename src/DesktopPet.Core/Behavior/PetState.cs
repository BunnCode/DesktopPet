using DesktopPet.Core.Abstractions;
using DesktopPet.Core.Animation;
using DesktopPet.Core.Simulation;

namespace DesktopPet.Core.Behavior;

public enum PetStateId
{
    Fall,
    Land,
    Idle,
    Walk,
    Sit,
    Held,
    Happy,
}

/// <summary>
/// One behavior. <see cref="Update"/> returns the next state to switch to, or null to stay.
/// Physics-driven transitions (Fall on losing ground, Land on touching it) are forced by the simulation, not returned here.
/// </summary>
internal abstract class PetState
{
    public abstract PetStateId Id { get; }

    public virtual void Enter(PetContext context)
    {
    }

    public abstract PetStateId? Update(PetContext context, double dt);
}

/// <summary>A voluntary behavior that lasts a random duration and then lets <see cref="BehaviorPicker"/> choose what's next.</summary>
internal abstract class TimedState : PetState
{
    private double _remaining;

    protected abstract AnimationDef Animation { get; }

    protected abstract DurationRange Duration(PetConfig config);

    public override void Enter(PetContext context)
    {
        var range = Duration(context.Config);
        _remaining = context.Random.Range(range.Min, range.Max);
        context.Animation.Play(Animation, restart: true);
    }

    public override PetStateId? Update(PetContext context, double dt)
    {
        Act(context, dt);
        _remaining -= dt;
        return _remaining <= 0 ? BehaviorPicker.Pick(context.Config, context.Random) : null;
    }

    protected virtual void Act(PetContext context, double dt)
    {
    }
}
