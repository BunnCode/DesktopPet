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

        var kick = errand.Use.Effect == UseEffect.Kick;
        if (kick && step.Move == RouteMove.Arrive
            && System.Math.Abs(item.Feet.X - body.Feet.X) <= context.HalfWidthPx + (item.Size.X / 2))
        {
            return PetStateId.UseFurniture; // a ball: close enough to kick is close enough
        }

        switch (RouteFollower.Follow(context, step, dt, snapToEnd: !kick))
        {
            case RouteProgress.Arrived:
                return PetStateId.UseFurniture;
            case RouteProgress.Climbing:
                return PetStateId.Climb;
            case RouteProgress.Blocked:
                return GiveUp(context);
            case RouteProgress.Replan:
                errand.Route = null; // the world changed under it: replan next tick
                return null;
            default:
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
    private double _minimum;
    private double _elapsed;
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
        errand.Item.Users[context.Owner] = use;
        _remaining = context.Random.Range(use.Seconds.Min, use.Seconds.Max);
        if (errand.PlaySeconds is { } cap)
        {
            _remaining = System.Math.Min(_remaining, cap); // joined someone's game: just for a little while
        }

        _minimum = use.MinSeconds;
        _remaining = System.Math.Max(_remaining, _minimum);
        _elapsed = 0;
        _settle = SettleSeconds;
        _hops = context.Config.Furniture.BounceHops;
        _cooldown = 0;
        _away = 0;
        context.Animation.Play(Animations.Find(use.Animation) ?? Animations.Idle, restart: true);

        // A seat that faces one way (the Living Tower's); otherwise, beside the furniture, face it.
        var offset = context.Body.Feet.X - errand.Item.Feet.X;
        var facing = errand.Item.Spec.FacingAt(errand.Slot);
        if (use.Effect != UseEffect.Kick && facing != SlotFacing.Toward)
        {
            context.FacingLeft = facing == SlotFacing.Left;
        }
        else if (use.Effect != UseEffect.Kick && System.Math.Abs(offset) > errand.Item.Size.X / 4)
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

        _elapsed += dt;
        var stats = context.Stats;
        var full = use.Need == Need.Energy
            ? stats.Energy >= context.Config.Stats.WellRested
            : use.Need != Need.None && FurnitureChooser.Value(use.Need, stats) >= FurnitureChooser.Full;
        full &= _elapsed >= _minimum; // stays its minimum (the Living Tower) even once the need is full
        if (full)
        {
            return Finish(context);
        }

        if (use.Effect != UseEffect.Bounce && (_remaining -= dt) <= 0)
        {
            return Done(context, errand);
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

    /// <summary>
    /// Used it for the whole time: a consumable piece is used up, and a breeding pill sends the pet round the others.
    /// </summary>
    private static PetStateId Done(PetContext context, Errand errand)
    {
        if (errand.Item.Spec.Consumable)
        {
            context.Furniture.Remove(errand.Item);
        }

        if (errand.Use.Effect == UseEffect.Breed && context.MayTakePill)
        {
            context.Visit = new Visit();
        }

        return Finish(context);
    }

    private static PetStateId Finish(PetContext context)
    {
        context.CancelErrand();
        return context.PickNext();
    }
}
