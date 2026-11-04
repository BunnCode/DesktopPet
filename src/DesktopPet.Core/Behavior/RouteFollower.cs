using DesktopPet.Core.Furniture;
using DesktopPet.Core.Physics;
using DesktopPet.Core.World;

namespace DesktopPet.Core.Behavior;

/// <summary>What one tick of following a route came to.</summary>
internal enum RouteProgress
{
    /// <summary>Still walking (or dropping off an end: physics takes it from there).</summary>
    Moving,

    /// <summary>At the end of the route.</summary>
    Arrived,

    /// <summary>A climb started: switch to <see cref="PetStateId.Climb"/>.</summary>
    Climbing,

    /// <summary>Can't walk on (the edge of the screen): give up.</summary>
    Blocked,

    /// <summary>The world changed under the step: plan again.</summary>
    Replan,
}

/// <summary>
/// Walks a pet along the first step of a <see cref="RoutePlanner"/> route: to the furniture it's heading for
/// (<see cref="GoToFurnitureState"/>) or to another pet (<see cref="VisitState"/>).
/// </summary>
internal static class RouteFollower
{
    /// <param name="snapToEnd">
    /// False to keep walking past the route's end rather than arrive there (chasing a ball, which decides for itself
    /// when it's close enough).
    /// </param>
    public static RouteProgress Follow(PetContext context, RouteStep step, double dt, bool snapToEnd = true)
    {
        var body = context.Body;
        var speed = context.Config.WalkSpeedDip * context.Scale;
        var feet = body.Feet;
        var snap = step.Move != RouteMove.WalkOff && (snapToEnd || step.Move != RouteMove.Arrive);
        if (snap && System.Math.Abs(step.X - feet.X) <= speed * dt)
        {
            body.Feet = feet with { X = step.X };
            return Reached(context, step);
        }

        var direction = step.Move == RouteMove.WalkOff ? step.Direction : System.Math.Sign(step.X - feet.X);
        if (direction == 0)
        {
            return RouteProgress.Moving;
        }

        context.FacingLeft = direction < 0;
        var dx = direction * speed * dt;
        if (step.Move != RouteMove.WalkOff && System.Math.Abs(dx) > System.Math.Abs(step.X - feet.X))
        {
            dx = step.X - feet.X;
        }

        if (!WorldQuery.IsWalkable(context.Monitors, feet.X + (direction * context.HalfWidthPx) + dx, feet.Y))
        {
            return RouteProgress.Blocked;
        }

        body.Feet = feet with { X = feet.X + dx };
        return RouteProgress.Moving;
    }

    /// <summary>Reached the step's spot: the end, or the start of a climb.</summary>
    private static RouteProgress Reached(PetContext context, RouteStep step)
    {
        var body = context.Body;
        var size = context.SizePx;
        switch (step.Move)
        {
            case RouteMove.Arrive:
                return RouteProgress.Arrived;

            case RouteMove.ClimbUp when PhysicsSystem.FindWall(context.Walls, step.WallKey, body.Feet.Y - (size / 2)) is { } wall:
                body.Cling(wall, body.Feet.Y, size);
                context.ClimbDirection = -1;
                return RouteProgress.Climbing;

            case RouteMove.ClimbDown when context.CurrentGround() is { } ground
                && RoutePlanner.SideAt(context.Walls, ground, step.Direction) is { } side:
                body.Cling(side, side.Top + (size * 0.75), size);
                context.ClimbDirection = 1;
                return RouteProgress.Climbing;

            default:
                return RouteProgress.Replan;
        }
    }

    /// <summary>How this pet gets about, for <see cref="RoutePlanner.Plan"/>.</summary>
    public static Walker Walker(PetContext context) =>
        new(context.HalfWidthPx, context.SizePx, context.Scale, context.Config.EnableClimbing, context.Config.GravityDip * context.Scale);
}
