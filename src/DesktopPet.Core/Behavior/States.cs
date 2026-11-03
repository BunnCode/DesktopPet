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

    public override PetStateId? Update(PetContext context, double dt) =>
        context.Animation.IsFinished ? BehaviorPicker.Pick(context.Config, context.Random) : null;
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
