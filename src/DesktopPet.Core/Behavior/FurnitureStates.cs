using DesktopPet.Core.Abstractions;
using DesktopPet.Core.Animation;
using DesktopPet.Core.Furniture;
using DesktopPet.Core.Math;
using DesktopPet.Core.Physics;
using DesktopPet.Core.World;

namespace DesktopPet.Core.Behavior;

/// <summary>
/// On the way to furniture (<see cref="PetContext.Errand"/>): follows the planned route, walking along surfaces and
/// handing over to <see cref="ClimbState"/> for climbs (which come back here at the top) and to physics for drops
/// (landing comes back here too). Replans as it goes, since windows and the furniture move. Gives up if there's no way
/// or it takes too long.
/// </summary>
internal sealed class GoToFurnitureState : PetState
{
    /// <summary>Seconds a pet waits for furniture that's being dragged or is falling.</summary>
    private const double MaxWaitSeconds = 5;

    public override PetStateId Id => PetStateId.GoToFurniture;

    public override void Enter(PetContext context) => context.Animation.Play(Animations.Walk);

    public override PetStateId? Update(PetContext context, double dt)
    {
        if (context.Errand is not { } errand)
        {
            return context.PickNext();
        }

        var body = context.Body;
        var item = errand.Item;
        errand.Elapsed += dt;
        if (item.IsRemoved)
        {
            context.CancelErrand();
            return context.PickNext();
        }

        if (errand.Elapsed > context.Config.Furniture.ErrandSeconds)
        {
            return GiveUp(context);
        }

        if (body.Mode != BodyMode.Grounded)
        {
            return null;
        }

        if (item.IsHeld || item.GroundKey is null)
        {
            // Being moved or still falling: wait for it.
            context.Animation.Play(Animations.Idle);
            errand.Waiting += dt;
            if (errand.Waiting > MaxWaitSeconds)
            {
                context.CancelErrand();
                return context.PickNext();
            }

            return null;
        }

        errand.Waiting = 0;
        context.Animation.Play(Animations.Walk);
        var target = FurnitureChooser.TargetX(context, errand);
        errand.ReplanIn -= dt;
        if (errand.Route is null || errand.PlannedFrom != body.GroundKey || errand.ReplanIn <= 0
            || target is not { } t || System.Math.Abs(t - errand.PlannedTarget) > 4 * context.Scale)
        {
            FurnitureChooser.Plan(context, errand);
        }

        if (errand.Route is not [var step, ..])
        {
            return GiveUp(context);
        }

        var speed = context.Config.WalkSpeedDip * context.Scale;
        var feet = body.Feet;

        if (step.Move == RouteMove.Arrive && errand.Use.Effect == UseEffect.Kick)
        {
            // A ball: close enough to kick is close enough.
            if (System.Math.Abs(item.Feet.X - feet.X) <= context.HalfWidthPx + (item.Size.X / 2))
            {
                return PetStateId.UseFurniture;
            }
        }
        else if (step.Move != RouteMove.WalkOff && System.Math.Abs(step.X - feet.X) <= speed * dt)
        {
            body.Feet = feet with { X = step.X };
            return Arrive(context, step);
        }

        var direction = step.Move == RouteMove.WalkOff ? step.Direction : System.Math.Sign(step.X - feet.X);
        if (direction == 0)
        {
            return null;
        }

        context.FacingLeft = direction < 0;
        var dx = direction * speed * dt;
        if (step.Move != RouteMove.WalkOff && System.Math.Abs(dx) > System.Math.Abs(step.X - feet.X))
        {
            dx = step.X - feet.X;
        }

        if (!WorldQuery.IsWalkable(context.Monitors, feet.X + (direction * context.HalfWidthPx) + dx, feet.Y))
        {
            return GiveUp(context);
        }

        body.Feet = feet with { X = feet.X + dx };
        return null;
    }

    /// <summary>Reached the step's spot: start using the furniture, or start the climb.</summary>
    private static PetStateId? Arrive(PetContext context, RouteStep step)
    {
        var body = context.Body;
        var size = context.SizePx;
        switch (step.Move)
        {
            case RouteMove.Arrive:
                return PetStateId.UseFurniture;

            case RouteMove.ClimbUp when PhysicsSystem.FindWall(context.Walls, step.WallKey, body.Feet.Y - (size / 2)) is { } wall:
                body.Cling(wall, body.Feet.Y, size);
                context.ClimbDirection = -1;
                return PetStateId.Climb;

            case RouteMove.ClimbDown when context.CurrentGround() is { } ground
                && RoutePlanner.SideAt(context.Walls, ground, step.Direction) is { } side:
                body.Cling(side, side.Top + (size * 0.75), size);
                context.ClimbDirection = 1;
                return PetStateId.Climb;

            default:
                context.Errand!.Route = null; // the world changed under it: replan next tick
                return null;
        }
    }

    private static PetStateId GiveUp(PetContext context)
    {
        if (context.Errand is { } errand)
        {
            context.Unreachable[errand.Item.Id] = context.Time + context.Config.Furniture.RetrySeconds;
        }

        context.CancelErrand();
        return context.PickNext();
    }
}

/// <summary>
/// Using furniture: plays the use's animation at its slot for the use's duration (ending early once the need is full),
/// while <see cref="Simulation.PetSimulation"/> adds the use's rate to the need. Effects: Hop hops on the spot, Bounce
/// hops and then launches the pet high, Kick chases the ball and kicks it around.
/// </summary>
internal sealed class UseFurnitureState : PetState
{
    private const double SettleSeconds = 0.3;
    private const double KickCooldownSeconds = 0.9;

    /// <summary>A ball on another surface this long: go after it.</summary>
    private const double BallAwaySeconds = 1.5;

    private double _remaining;
    private double _settle;
    private int _hops;
    private double _cooldown;
    private double _away;

    public override PetStateId Id => PetStateId.UseFurniture;

    public override void Enter(PetContext context)
    {
        if (context.Errand is not { } errand)
        {
            return;
        }

        var use = errand.Use;
        context.ActiveUse = use;
        errand.Item.Users.Add(context.Owner);
        _remaining = context.Random.Range(use.Seconds.Min, use.Seconds.Max);
        _settle = SettleSeconds;
        _hops = context.Config.Furniture.BounceHops;
        _cooldown = 0;
        _away = 0;
        context.Animation.Play(Animations.Find(use.Animation) ?? Animations.Idle, restart: true);

        // Beside the furniture: face it.
        var offset = context.Body.Feet.X - errand.Item.Feet.X;
        if (use.Effect != UseEffect.Kick && System.Math.Abs(offset) > errand.Item.Size.X / 4)
        {
            context.FacingLeft = offset > 0;
        }
    }

    public override void Exit(PetContext context)
    {
        context.ActiveUse = null;
        context.Errand?.Item.Users.Remove(context.Owner);
    }

    public override PetStateId? Update(PetContext context, double dt)
    {
        if (context.Errand is not { } errand)
        {
            return context.PickNext();
        }

        var item = errand.Item;
        var use = errand.Use;
        if (item.IsRemoved || item.IsHeld)
        {
            return Finish(context);
        }

        var stats = context.Stats;
        var full = use.Need == Need.Energy
            ? stats.Energy >= context.Config.Stats.WellRested
            : use.Need != Need.None && FurnitureChooser.Value(use.Need, stats) >= FurnitureChooser.Full;
        if (full || (use.Effect != UseEffect.Bounce && (_remaining -= dt) <= 0))
        {
            return Finish(context);
        }

        return use.Effect switch
        {
            UseEffect.Kick => Kick(context, item, dt),
            UseEffect.Hop or UseEffect.Bounce => Hop(context, errand, dt),
            _ => StayAtSlot(context, errand),
        };
    }

    /// <summary>The furniture moved away from under it (not riding along): walk back to the slot.</summary>
    private static PetStateId? StayAtSlot(PetContext context, Errand errand)
    {
        var body = context.Body;
        if (body.Mode != BodyMode.Grounded)
        {
            return null;
        }

        var target = FurnitureChooser.TargetX(context, errand);
        return target is not { } x || body.GroundKey != errand.Item.GroundKey || System.Math.Abs(body.Feet.X - x) > 3 * context.Scale
            ? PetStateId.GoToFurniture
            : null;
    }

    private PetStateId? Hop(PetContext context, Errand errand, double dt)
    {
        var body = context.Body;
        if (body.Mode != BodyMode.Grounded)
        {
            _settle = SettleSeconds;
            return body.Mode == BodyMode.Airborne ? null : Finish(context);
        }

        if (StayAtSlot(context, errand) is { } walkBack)
        {
            return walkBack;
        }

        if ((_settle -= dt) > 0)
        {
            return null;
        }

        var rules = context.Config.Furniture;
        if (errand.Use.Effect == UseEffect.Bounce && _hops-- <= 0)
        {
            // The big one: straight up, maybe onto a tall window top.
            body.Release(new Vec2(0, -rules.BounceLaunchDip * context.Scale));
            context.CancelErrand();
            return PetStateId.Fall;
        }

        var speed = errand.Use.Effect == UseEffect.Bounce ? rules.BounceHopSpeedDip : rules.HopSpeedDip;
        body.Release(new Vec2(0, -speed * context.Scale));
        return null;
    }

    /// <summary>Chase the ball along the surface and kick it whenever it's in reach.</summary>
    private PetStateId? Kick(PetContext context, FurnitureItem ball, double dt)
    {
        var body = context.Body;
        if (body.Mode != BodyMode.Grounded)
        {
            return null;
        }

        _cooldown -= dt;
        if (ball.GroundKey is null)
        {
            // In the air: watch it.
            if (_cooldown <= 0)
            {
                context.Animation.Play(Animations.Idle);
            }

            context.FacingLeft = ball.Feet.X < body.Feet.X;
            return null;
        }

        if (ball.GroundKey != body.GroundKey)
        {
            context.Animation.Play(Animations.Idle);
            return (_away += dt) > BallAwaySeconds ? PetStateId.GoToFurniture : null;
        }

        _away = 0;
        var gap = ball.Feet.X - body.Feet.X;
        var direction = gap < 0 ? -1 : 1;
        context.FacingLeft = direction < 0;
        if (System.Math.Abs(gap) > context.HalfWidthPx + (ball.Size.X / 2))
        {
            if (_cooldown > 0)
            {
                return null; // finishing the kick
            }

            context.Animation.Play(Animations.Walk);
            var dx = direction * context.Config.WalkSpeedDip * 1.5 * context.Scale * dt; // chasing: a bit faster
            if (WorldQuery.IsWalkable(context.Monitors, body.Feet.X + (direction * context.HalfWidthPx) + dx, body.Feet.Y)
                && context.Furniture.PenAhead(body.GroundKey, body.Feet.X + (direction * context.HalfWidthPx), body.Feet.X + (direction * context.HalfWidthPx) + dx) is null)
            {
                body.Feet = body.Feet with { X = body.Feet.X + dx };
            }

            return null;
        }

        if (_cooldown <= 0)
        {
            var rules = context.Config.Furniture;
            var random = context.Random;
            FurnitureWorld.Kick(ball, new Vec2(
                direction * rules.KickSpeedDip * random.Range(0.7, 1.3) * context.Scale,
                -rules.KickLiftDip * random.Range(0.6, 1.2) * context.Scale));
            context.Animation.Play(Animations.Kick, restart: true);
            _cooldown = KickCooldownSeconds;
        }

        return null;
    }

    private static PetStateId Finish(PetContext context)
    {
        context.CancelErrand();
        return context.PickNext();
    }
}
