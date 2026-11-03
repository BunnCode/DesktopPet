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

/// <summary>Hanging from a title bar. When it's had enough: keep hanging, shimmy along, or let go.</summary>
internal sealed class HangState : TimedState
{
    public override PetStateId Id => PetStateId.Hang;
    protected override AnimationDef Animation => Animations.Hang;
    protected override DurationRange Duration(PetConfig config) => config.HangSeconds;

    protected override PetStateId Next(PetContext context)
    {
        var roll = context.Random.NextDouble();
        if (roll < context.Config.HangShimmyChance)
        {
            return PetStateId.Shimmy;
        }

        if (roll < context.Config.HangShimmyChance + context.Config.HangDropChance)
        {
            context.Body.Release(Math.Vec2.Zero);
            return PetStateId.Fall;
        }

        return PetStateId.Hang;
    }
}

/// <summary>Moves hand over hand along the bar, turning back before running out of bar.</summary>
internal sealed class ShimmyState : TimedState
{
    public override PetStateId Id => PetStateId.Shimmy;
    protected override AnimationDef Animation => Animations.Shimmy;
    protected override DurationRange Duration(PetConfig config) => config.ShimmySeconds;

    public override void Enter(PetContext context)
    {
        base.Enter(context);
        if (context.Random.Chance(0.5))
        {
            context.FacingLeft = !context.FacingLeft;
        }
    }

    protected override PetStateId Next(PetContext context) => PetStateId.Hang;

    protected override void Act(PetContext context, double dt)
    {
        var body = context.Body;
        if (body.Mode != BodyMode.Hanging || CurrentBar(context) is not { } bar)
        {
            return;
        }

        var dx = context.Direction * context.Config.ShimmySpeedDip * context.Scale * dt;
        var margin = context.HalfWidthPx * 0.5; // keep both hands on the bar
        var next = body.Feet.X + dx;
        if (next < bar.Left + margin || next >= bar.Right - margin)
        {
            context.FacingLeft = !context.FacingLeft;
            return;
        }

        body.Feet = body.Feet with { X = next };
    }

    private static Platform? CurrentBar(PetContext context)
    {
        foreach (var p in context.Platforms)
        {
            if (p.Key == context.Body.GroundKey && p.SpansX(context.Body.Feet.X))
            {
                return p;
            }
        }

        return null;
    }
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
