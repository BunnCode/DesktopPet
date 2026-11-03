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
            ? BehaviorPicker.Pick(context.Config, context.Random, context.Stats)
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

    protected override PetStateId? Act(PetContext context, double dt)
    {
        var body = context.Body;
        if (body.Mode != BodyMode.Hanging || CurrentBar(context) is not { } bar)
        {
            return null;
        }

        var dx = context.Direction * context.Config.ShimmySpeedDip * context.Scale * dt;
        var margin = context.HalfWidthPx * 0.5; // keep both hands on the bar
        var next = body.Feet.X + dx;
        if (next < bar.Left + margin || next >= bar.Right - margin)
        {
            context.FacingLeft = !context.FacingLeft;
            return null;
        }

        body.Feet = body.Feet with { X = next };
        return null;
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

    public override void Enter(PetContext context)
    {
        base.Enter(context);
        if (context.Stats.Happiness < context.Config.Stats.Sad || context.Stats.Lowest < context.Config.Stats.Critical)
        {
            context.Animation.Play(Animations.IdleSad, restart: true);
        }
    }
}

/// <summary>
/// Asleep. A self-chosen nap lasts a while; when tucked in from the menu it sleeps until rested.
/// Either way it wakes once energy is back up. Clicking it wakes it (handled by the simulation).
/// </summary>
internal sealed class SleepState : TimedState
{
    private static readonly DurationRange TuckedIn = new(30 * 60, 30 * 60);

    public override PetStateId Id => PetStateId.Sleep;
    protected override AnimationDef Animation => Animations.Sleep;
    protected override DurationRange Duration(PetConfig config) => config.NapSeconds;

    public override void Enter(PetContext context)
    {
        base.Enter(context);
        if (context.TuckedIn)
        {
            Extend(context, TuckedIn);
        }
    }

    public override PetStateId? Update(PetContext context, double dt)
    {
        if (context.Stats.Energy >= context.Config.Stats.WellRested)
        {
            context.TuckedIn = false;
            return Next(context);
        }

        var next = base.Update(context, dt);
        if (next is not null)
        {
            context.TuckedIn = false;
        }

        return next;
    }
}

/// <summary>Munching the snack it was just given.</summary>
internal sealed class EatState : TimedState
{
    public override PetStateId Id => PetStateId.Eat;
    protected override AnimationDef Animation => Animations.Eat;
    protected override DurationRange Duration(PetConfig config) => new(config.EatSeconds, config.EatSeconds);
}

/// <summary>Playing: a few real hops (physics jumps), then back to normal life.</summary>
internal sealed class PlayState : PetState
{
    private const double SettleSeconds = 0.35;
    private int _hopsLeft;
    private double _settle;

    public override PetStateId Id => PetStateId.Play;

    public override void Enter(PetContext context)
    {
        _hopsLeft = context.Config.PlayHops;
        _settle = 0.15;
        context.Animation.Play(Animations.Happy, restart: true);
    }

    public override PetStateId? Update(PetContext context, double dt)
    {
        var body = context.Body;
        if (body.Mode != BodyMode.Grounded)
        {
            _settle = SettleSeconds;
            return body.Mode == BodyMode.Airborne ? null : BehaviorPicker.Pick(context.Config, context.Random, context.Stats);
        }

        if ((_settle -= dt) > 0)
        {
            return null;
        }

        if (_hopsLeft-- > 0)
        {
            body.Release(new Math.Vec2(0, -context.Config.PlayHopSpeedDip * context.Scale));
            return null;
        }

        return BehaviorPicker.Pick(context.Config, context.Random, context.Stats);
    }
}

/// <summary>Sits down; on a window's title bar it perches on the edge with its legs dangling over.</summary>
internal sealed class SitState : TimedState
{
    public override PetStateId Id => PetStateId.Sit;
    protected override AnimationDef Animation => Animations.Sit;
    protected override DurationRange Duration(PetConfig config) => config.SitSeconds;

    public override void Enter(PetContext context)
    {
        base.Enter(context);
        if (context.Body.Mode == BodyMode.Grounded && context.CurrentGround() is { Kind: PlatformKind.WindowTop })
        {
            context.Animation.Play(Animations.SitLedge, restart: true);
        }
    }
}

/// <summary>
/// Walks along the ground. At a wall it may climb it, push it (window sides), or else turn (screen edges) or walk on
/// past (windows are in the background). At the end of a window top it may climb down that window's side.
/// </summary>
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

    protected override PetStateId? Act(PetContext context, double dt)
    {
        var body = context.Body;
        if (body.Mode != BodyMode.Grounded)
        {
            return null;
        }

        var config = context.Config;
        var dx = context.Direction * config.WalkSpeedDip * context.Scale * dt;
        var size = context.SizePx;

        if (WallAhead(context, dx) is { } wall)
        {
            var roll = context.Random.NextDouble();
            if (config.EnableClimbing && roll < config.ClimbChance)
            {
                body.Cling(wall, body.Feet.Y, size);
                context.ClimbDirection = -1;
                return PetStateId.Climb;
            }

            if (context.PushingEnabled && wall is { Kind: WallKind.WindowSide, WindowId: { } id }
                && roll < config.ClimbChance + config.PushChance && !context.ImmovableWindows.Contains(id))
            {
                context.PushWallKey = wall.Key;
                return PetStateId.Push;
            }

            if (wall.Kind == WallKind.ScreenEdge)
            {
                context.FacingLeft = !context.FacingLeft;
                return null;
            }

            // A window side it chose not to climb or push: windows are background, walk on past.
        }

        if (config.EnableClimbing && SideToClimbDown(context, dx) is { } side && context.Random.Chance(config.ClimbDownChance))
        {
            body.Cling(side, side.Top + (size * 0.75), size);
            context.ClimbDirection = 1;
            return PetStateId.Climb;
        }

        var leadingEdge = body.Feet.X + (context.Direction * context.HalfWidthPx) + dx;
        if (!WorldQuery.IsWalkable(context.Monitors, leadingEdge, body.Feet.Y))
        {
            context.FacingLeft = !context.FacingLeft;
            return null;
        }

        body.Feet = body.Feet with { X = body.Feet.X + dx };
        return null;
    }

    /// <summary>A wall the leading edge reaches this step that's tall enough beside the pet to matter.</summary>
    private static Wall? WallAhead(PetContext context, double dx)
    {
        if (!context.Config.EnableClimbing && !context.PushingEnabled)
        {
            return null;
        }

        var feet = context.Body.Feet;
        var size = context.SizePx;
        var wall = PhysicsSystem.FindWallCrossing(context.Walls, feet.X, feet.X + dx, feet.Y - (size / 2), context.HalfWidthPx);

        // It must run alongside the pet from (nearly) its feet to above its middle.
        return wall is { } w && w.Top <= feet.Y - (size * 0.75) && w.Bottom >= feet.Y - 2 ? w : null;
    }

    /// <summary>About to walk off the end of a window top: that window's outside face on this end, starting at the top.</summary>
    private static Wall? SideToClimbDown(PetContext context, double dx)
    {
        if (context.CurrentGround() is not { Kind: PlatformKind.WindowTop } ground || ground.SpansX(context.Body.Feet.X + dx))
        {
            return null;
        }

        var edgeX = dx > 0 ? ground.Right : ground.Left;
        foreach (var w in context.Walls)
        {
            if (w.Kind == WallKind.WindowSide && w.TowardWall == -System.Math.Sign(dx)
                && System.Math.Abs(w.X - edgeX) <= 2 && System.Math.Abs(w.Top - ground.Y) <= 2)
            {
                return w;
            }
        }

        return null;
    }
}

/// <summary>Shared choices for a pet on a wall.</summary>
internal static class WallBehavior
{
    /// <summary>Climb up (most likely), climb down, hold on, or let go and drop.</summary>
    public static PetStateId Pick(PetContext context)
    {
        var roll = context.Random.NextDouble();
        if (roll < 0.35)
        {
            context.ClimbDirection = -1;
            return PetStateId.Climb;
        }

        if (roll < 0.55)
        {
            context.ClimbDirection = 1;
            return PetStateId.Climb;
        }

        if (roll < 0.85)
        {
            return PetStateId.Cling;
        }

        return LetGo(context);
    }

    /// <summary>Pushes off the wall a little and falls.</summary>
    public static PetStateId LetGo(PetContext context)
    {
        var away = context.CurrentWall() is { } w ? -w.TowardWall : 0;
        context.Body.Release(new Math.Vec2(away * 120 * context.Scale, 0));
        return PetStateId.Fall;
    }

    public static void FaceWall(PetContext context)
    {
        if (context.CurrentWall() is { } w)
        {
            context.FacingLeft = w.TowardWall < 0;
        }
    }
}

/// <summary>
/// Climbing a wall. At the top it climbs over onto whatever surface is there (a window's title bar, a higher floor)
/// or turns back; at the bottom it drops off.
/// </summary>
internal sealed class ClimbState : TimedState
{
    public override PetStateId Id => PetStateId.Climb;
    protected override AnimationDef Animation => Animations.Climb;
    protected override DurationRange Duration(PetConfig config) => config.ClimbSeconds;

    public override void Enter(PetContext context)
    {
        base.Enter(context);
        WallBehavior.FaceWall(context);
    }

    protected override PetStateId Next(PetContext context) => WallBehavior.Pick(context);

    protected override PetStateId? Act(PetContext context, double dt)
    {
        var body = context.Body;
        if (body.Mode != BodyMode.Clinging || context.CurrentWall() is not { } wall)
        {
            return null;
        }

        var size = context.SizePx;
        var feetY = body.Feet.Y + (context.ClimbDirection * context.Config.ClimbSpeedDip * context.Scale * dt);

        if (context.ClimbDirection < 0 && feetY - (size / 2) <= wall.Top)
        {
            // Mid-body reached the top: climb over onto a surface there, or turn back down.
            var overX = wall.X + (wall.TowardWall * (context.HalfWidthPx + 2));
            foreach (var p in context.Platforms)
            {
                if (p.IsGround && p.SpansX(overX) && System.Math.Abs(p.Y - wall.Top) <= 2)
                {
                    body.Land(overX, p);
                    context.FacingLeft = wall.TowardWall < 0;
                    return PetStateId.Idle;
                }
            }

            context.ClimbDirection = 1;
            return null;
        }

        if (context.ClimbDirection > 0 && feetY >= wall.Bottom)
        {
            return WallBehavior.LetGo(context);
        }

        body.Feet = body.Feet with { Y = feetY };
        return null;
    }
}

/// <summary>Holding still on a wall.</summary>
internal sealed class ClingState : TimedState
{
    public override PetStateId Id => PetStateId.Cling;
    protected override AnimationDef Animation => Animations.Cling;
    protected override DurationRange Duration(PetConfig config) => config.ClingSeconds;

    public override void Enter(PetContext context)
    {
        base.Enter(context);
        WallBehavior.FaceWall(context);
    }

    protected override PetStateId Next(PetContext context) => WallBehavior.Pick(context);
}

/// <summary>
/// Leaning into a window's side and pushing it along by issuing <see cref="WindowCommand"/>s. The pet stays against
/// the wall wherever the window actually is, so if the window doesn't move it doesn't either. Never pushes a window
/// past the edge of its screen; gives up on windows that won't budge.
/// </summary>
internal sealed class PushState : TimedState
{
    private const double StuckAfterSeconds = 0.6;
    private string? _key;
    private double _startX;
    private double _pending;
    private double _commanded;
    private double _elapsed;

    public override PetStateId Id => PetStateId.Push;
    protected override AnimationDef Animation => Animations.Push;
    protected override DurationRange Duration(PetConfig config) => config.PushSeconds;

    public override void Enter(PetContext context)
    {
        base.Enter(context);
        _key = context.PushWallKey;
        _startX = context.CurrentWall(_key)?.X ?? 0;
        _pending = _commanded = _elapsed = 0;
    }

    protected override PetStateId? Act(PetContext context, double dt)
    {
        var body = context.Body;
        if (body.Mode != BodyMode.Grounded)
        {
            return null;
        }

        if (!context.PushingEnabled)
        {
            return Next(context); // switched off mid-push
        }

        if (context.CurrentWall(_key) is not { WindowId: { } id } wall)
        {
            return Next(context); // window gone or covered
        }

        var direction = wall.TowardWall;
        context.FacingLeft = direction < 0;
        body.Feet = body.Feet with { X = wall.X - (direction * context.HalfWidthPx) }; // stay against the window

        if (!RoomToPush(context, wall, direction))
        {
            context.FacingLeft = !context.FacingLeft;
            return PetStateId.Walk;
        }

        _elapsed += dt;
        if (_elapsed >= StuckAfterSeconds && _commanded >= 10 && System.Math.Abs(wall.X - _startX) < 2)
        {
            context.ImmovableWindows.Add(id); // e.g. an elevated app; we're not allowed to move it
            context.FacingLeft = !context.FacingLeft;
            return PetStateId.Walk;
        }

        _pending += direction * context.Config.PushSpeedDip * context.Scale * dt;
        var step = (int)_pending;
        if (step != 0)
        {
            context.Commands.Add(new Simulation.WindowCommand(id, step, 0));
            _pending -= step;
            _commanded += System.Math.Abs(step);
        }

        return null;
    }

    /// <summary>Keeps the window entirely within the work area of the monitor it's on.</summary>
    private static bool RoomToPush(PetContext context, Wall wall, int direction)
    {
        var owner = wall.Owner;
        var centre = new Math.Vec2(owner.Left + (owner.Width / 2.0), owner.Top + (owner.Height / 2.0));
        var monitor = context.Monitors.FirstOrDefault(m => m.WorkArea.Contains(centre)) ?? WorldQuery.Nearest(context.Monitors, centre);
        if (monitor is null)
        {
            return false;
        }

        return direction > 0 ? owner.Right + 1 <= monitor.WorkArea.Right : owner.Left - 1 >= monitor.WorkArea.Left;
    }
}
