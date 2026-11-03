using DesktopPet.Core.Abstractions;
using DesktopPet.Core.Animation;
using DesktopPet.Core.Physics;
using DesktopPet.Core.Simulation;
using DesktopPet.Core.World;

namespace DesktopPet.Core.Behavior;

internal sealed class FallState : PetState
{
    public override PetStateId Id => PetStateId.Fall;

    public override void Enter(PetContext context) => context.Animation.Play(Animations.Fall);

    // Left only when physics reports a landing.
    public override PetStateId? Update(PetContext context, double dt) => null;
}

internal sealed class LandState : PetState
{
    public override PetStateId Id => PetStateId.Land;

    public override void Enter(PetContext context) => context.Animation.Play(Animations.Land, restart: true);

    // Waits for the squash to play out and for any skid from a throw to stop.
    public override PetStateId? Update(PetContext context, double dt) =>
        context.Animation.IsFinished && context.Body.Velocity.X == 0
            ? BehaviorPicker.Pick(context.Config, context.Random)
            : null;
}

/// <summary>Dangling from the mouse. Left by the simulation on release (→ Fall).</summary>
internal sealed class HeldState : PetState
{
    public override PetStateId Id => PetStateId.Held;

    public override void Enter(PetContext context) => context.Animation.Play(Animations.Held);

    public override PetStateId? Update(PetContext context, double dt) => null;
}

/// <summary>Reaction to being clicked ("petted").</summary>
internal sealed class HappyState : TimedState
{
    public override PetStateId Id => PetStateId.Happy;
    protected override AnimationDef Animation => Animations.Happy;
    protected override DurationRange Duration(PetConfig config) => config.HappySeconds;
}

internal sealed class IdleState : TimedState
{
    public override PetStateId Id => PetStateId.Idle;
    protected override AnimationDef Animation => Animations.Idle;
    protected override DurationRange Duration(PetConfig config) => config.IdleSeconds;
}

internal sealed class SitState : TimedState
{
    public override PetStateId Id => PetStateId.Sit;
    protected override AnimationDef Animation => Animations.Sit;
    protected override DurationRange Duration(PetConfig config) => config.SitSeconds;
}

/// <summary>Walks along the ground, turning around at walls (see <see cref="WorldQuery.IsWalkable"/>).</summary>
internal sealed class WalkState : TimedState
{
    public override PetStateId Id => PetStateId.Walk;
    protected override AnimationDef Animation => Animations.Walk;
    protected override DurationRange Duration(PetConfig config) => config.WalkSeconds;

    public override void Enter(PetContext context)
    {
        base.Enter(context);
        if (context.Random.Chance(context.Config.WalkTurnChance))
        {
            context.FacingLeft = !context.FacingLeft;
        }
    }

    protected override void Act(PetContext context, double dt)
    {
        var body = context.Body;
        if (body.Mode != BodyMode.Grounded)
        {
            return;
        }

        var dx = context.Direction * context.Config.WalkSpeedDip * context.Scale * dt;
        var leadingEdge = body.Feet.X + (context.Direction * context.HalfWidthPx) + dx;

        if (!WorldQuery.IsWalkable(context.Monitors, leadingEdge, body.Feet.Y))
        {
            context.FacingLeft = !context.FacingLeft;
            return;
        }

        body.Feet = body.Feet with { X = body.Feet.X + dx };
    }
}
