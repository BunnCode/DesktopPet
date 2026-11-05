using Deskling.Core.Animation;
using Deskling.Core.Furniture;
using Deskling.Core.Physics;

namespace Deskling.Core.Behavior;

/// <summary>
/// A pet's round of the other pets after swallowing a breeding pill (M15). <see cref="Breeding.BreedingDirector"/>
/// says where to go (beside the pet it's asking next) and when to ask.
/// </summary>
internal sealed class Visit
{
    /// <summary>Seconds without a way there before <see cref="Blocked"/> says so.</summary>
    private const double BlockedAfterSeconds = 2;

    /// <summary>The surface to go to; null while it waits to be told who's next.</summary>
    public string? TargetKey { get; set; }

    /// <summary>Where to stand there (feet x, physical pixels).</summary>
    public double TargetX { get; set; }

    /// <summary>Asking: facing the other pet, gazing with hearts, until it answers.</summary>
    public bool Asking { get; set; }

    public bool AskFaceLeft { get; set; }

    /// <summary>Seconds it has had no way to the target.</summary>
    public double BlockedSeconds { get; set; }

    public bool Blocked => BlockedSeconds >= BlockedAfterSeconds;

    public IReadOnlyList<RouteStep>? Route { get; set; }

    public string? PlannedFrom { get; set; }

    public double PlannedTarget { get; set; }

    public string? PlannedTo { get; set; }

    public double ReplanIn { get; set; }
}

/// <summary>
/// On a pill round (<see cref="PetContext.Visit"/>): follows a planned route to the spot the director gives, like
/// <see cref="GoToFurnitureState"/> (climbs and drops come back here), waits there, and asks when told to. Ends only
/// when the round does.
/// </summary>
internal sealed class VisitState : PetState
{
    public override PetStateId Id => PetStateId.Visit;

    public override void Enter(PetContext context) => context.Animation.Play(Animations.Walk);

    public override PetStateId? Update(PetContext context, double dt)
    {
        if (context.Visit is not { } visit)
        {
            return context.PickNext();
        }

        var body = context.Body;
        if (body.Mode != BodyMode.Grounded)
        {
            return null;
        }

        if (visit.Asking)
        {
            context.FacingLeft = visit.AskFaceLeft;
            context.Animation.Play(Animations.Love);
            return null;
        }

        if (visit.TargetKey is not { } key || context.Pen is not null)
        {
            context.Animation.Play(Animations.Idle); // waiting for who's next, or shut in
            return null;
        }

        var x = Reachable(context, key, visit.TargetX);
        if (body.GroundKey == key && System.Math.Abs(body.Feet.X - x) <= 3 * context.Scale)
        {
            visit.BlockedSeconds = 0;
            context.Animation.Play(Animations.Idle); // there: the director asks
            return null;
        }

        visit.ReplanIn -= dt;
        if (visit.Route is null || visit.PlannedFrom != body.GroundKey || visit.PlannedTo != key || visit.ReplanIn <= 0
            || System.Math.Abs(visit.TargetX - visit.PlannedTarget) > 4 * context.Scale)
        {
            Plan(context, visit, key, x);
        }

        if (visit.Route is not [var step, ..])
        {
            visit.BlockedSeconds += dt;
            context.Animation.Play(Animations.Idle);
            return null;
        }

        context.Animation.Play(Animations.Walk);
        switch (RouteFollower.Follow(context, step, dt))
        {
            case RouteProgress.Climbing:
                return PetStateId.Climb;
            case RouteProgress.Arrived:
                visit.BlockedSeconds = 0;
                context.Animation.Play(Animations.Idle);
                return null;
            case RouteProgress.Blocked:
                visit.BlockedSeconds += dt;
                visit.Route = null;
                return null;
            case RouteProgress.Replan:
                visit.Route = null;
                return null;
            default:
                visit.BlockedSeconds = 0;
                return null;
        }
    }

    private static void Plan(PetContext context, Visit visit, string to, double x)
    {
        var body = context.Body;
        visit.PlannedFrom = body.GroundKey;
        visit.PlannedTo = to;
        visit.PlannedTarget = visit.TargetX;
        visit.ReplanIn = 0.5;
        visit.Route = body.GroundKey is { } from
            ? RoutePlanner.Plan(context.Platforms, context.Walls, context.Monitors, from, body.Feet.X, to, x, RouteFollower.Walker(context))
            : null;
    }

    /// <summary>The target x kept on its surface (beside a pet at the end of a window top: as close as it gets).</summary>
    private static double Reachable(PetContext context, string key, double x)
    {
        var best = x;
        var bestGap = double.MaxValue;
        foreach (var p in context.Platforms)
        {
            if (p.Key != key || !p.IsGround)
            {
                continue;
            }

            var clamped = System.Math.Clamp(x, p.Left + 1, p.Right - 1);
            if (System.Math.Abs(clamped - x) < bestGap)
            {
                best = clamped;
                bestGap = System.Math.Abs(clamped - x);
            }
        }

        return best;
    }
}
