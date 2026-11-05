using Deskling.Core.Animation;
using Deskling.Core.Physics;
using Deskling.Core.World;

namespace Deskling.Core.Behavior;

/// <summary>One pet's side of a courtship, set up by <see cref="Breeding.BreedingDirector"/>.</summary>
/// <param name="MeetX">Where it walks to (feet x, physical pixels), beside its partner.</param>
/// <param name="FaceLeft">Which way it faces once there: towards its partner.</param>
/// <param name="GiveUpSeconds">It stops courting on its own after this long, in case nobody ends it.</param>
internal sealed record Courtship(double MeetX, bool FaceLeft, double GiveUpSeconds)
{
    /// <summary>It got there (or as near as it could) and is gazing at its partner.</summary>
    public bool Arrived { get; set; }
}

/// <summary>
/// Courting: walks along its surface up to its partner, then faces it and gazes with hearts (<see cref="Animations.Love"/>)
/// until the director ends the courtship (an egg, or called off). Losing the ground, being picked up or anything else
/// that changes its state calls it off.
/// </summary>
internal sealed class CourtState : PetState
{
    private double _elapsed;

    public override PetStateId Id => PetStateId.Court;

    public override void Enter(PetContext context)
    {
        _elapsed = 0;
        context.Animation.Play(context.Courtship is { Arrived: true } ? Animations.Love : Animations.Walk, restart: true);
    }

    public override void Exit(PetContext context) => context.Courtship = null;

    public override PetStateId? Update(PetContext context, double dt)
    {
        var body = context.Body;
        _elapsed += dt;
        if (context.Courtship is not { } courtship || body.Mode != BodyMode.Grounded || _elapsed > courtship.GiveUpSeconds)
        {
            return body.Mode == BodyMode.Grounded ? context.PickNext() : null;
        }

        if (courtship.Arrived)
        {
            return null;
        }

        var gap = courtship.MeetX - body.Feet.X;
        var step = context.Config.WalkSpeedDip * context.Scale * dt;
        if (System.Math.Abs(gap) <= step)
        {
            body.Feet = body.Feet with { X = courtship.MeetX };
            Arrive(context, courtship);
            return null;
        }

        var dx = System.Math.Sign(gap) * step;
        context.FacingLeft = dx < 0;
        var edge = body.Feet.X + (context.Direction * context.HalfWidthPx);
        if (context.Furniture.PenAhead(body.GroundKey, edge, edge + dx) is not null
            || !WorldQuery.IsWalkable(context.Monitors, edge + dx, body.Feet.Y))
        {
            Arrive(context, courtship); // as near as it can get
            return null;
        }

        body.Feet = body.Feet with { X = body.Feet.X + dx };
        return null;
    }

    private static void Arrive(PetContext context, Courtship courtship)
    {
        courtship.Arrived = true;
        context.FacingLeft = courtship.FaceLeft;
        context.Animation.Play(Animations.Love, restart: true);
    }
}
