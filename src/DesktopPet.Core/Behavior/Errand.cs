using DesktopPet.Core.Furniture;
using DesktopPet.Core.Simulation;
using DesktopPet.Core.Stats;

namespace DesktopPet.Core.Behavior;

/// <summary>A pet's trip to a piece of furniture: which one, what for, its slot there, and the way.</summary>
internal sealed class Errand(FurnitureItem item, FurnitureUse use, int slot)
{
    public FurnitureItem Item { get; } = item;

    public FurnitureUse Use { get; } = use;

    public int Slot { get; } = slot;

    /// <summary>Seconds since it set off.</summary>
    public double Elapsed { get; set; }

    /// <summary>Seconds the furniture has been out of reach (held, falling) while the pet waits.</summary>
    public double Waiting { get; set; }

    public IReadOnlyList<RouteStep>? Route { get; set; }

    /// <summary>The surface and target the route was planned from, to replan when either changes.</summary>
    public string? PlannedFrom { get; set; }

    public double PlannedTarget { get; set; }

    public double ReplanIn { get; set; }
}

/// <summary>How pets choose furniture: by how low the need it meets is, and whether they can get there.</summary>
internal static class FurnitureChooser
{
    /// <summary>A need counts as met at this value (a use ends early).</summary>
    public const double Full = 99.5;

    /// <summary>How tempting a breeding pill is, as if a need it meets were this urgent (0..1).</summary>
    public const double PillAppeal = 0.5;

    /// <summary>0 when the need is at or above <see cref="FurnitureRules.NeedBelow"/>, rising to 1 when it's empty.</summary>
    public static double Urgency(Need need, PetStats stats, FurnitureRules rules)
    {
        if (need == Need.None || rules.NeedBelow <= 0)
        {
            return 0;
        }

        var value = Value(need, stats);
        return System.Math.Clamp((rules.NeedBelow - value) / rules.NeedBelow, 0, 1);
    }

    public static double Value(Need need, PetStats stats) => need switch
    {
        Need.Hunger => stats.Hunger,
        Need.Happiness => stats.Happiness,
        Need.Energy => stats.Energy,
        _ => PetStats.Max,
    };

    /// <summary>The weight of "go use furniture" for <see cref="BehaviorPicker"/>: 0 when there's nothing worth using.</summary>
    public static double Weight(PetContext context)
    {
        if (!CanChoose(context))
        {
            return 0;
        }

        var rules = context.Config.Furniture;
        var urgency = 0.0;
        var fun = false;
        foreach (var (_, use, score, curious) in Candidates(context))
        {
            urgency = System.Math.Max(urgency, curious ? 0 : score);
            fun |= curious || use.Need == Need.Happiness;
        }

        return (rules.Weight * urgency) + (fun ? rules.CuriosityWeight : 0);
    }

    /// <summary>
    /// Picks the most useful piece of furniture it can get to, reserves a slot there and sets off
    /// (<see cref="PetContext.Errand"/>). False if there's none.
    /// </summary>
    public static bool TryStart(PetContext context)
    {
        if (!CanChoose(context))
        {
            return false;
        }

        // Most urgent first; among equals the nearest, with a little randomness so pets don't all pick the same one.
        var feetX = context.Body.Feet.X;
        var ranked = Candidates(context)
            .Select(c => (c.Item, c.Use, Rank: c.Score - (System.Math.Abs(c.Item.Feet.X - feetX) / 20_000) + (context.Random.NextDouble() * 0.05)))
            .OrderByDescending(c => c.Rank)
            .Take(4)
            .ToList();

        foreach (var (item, use, _) in ranked)
        {
            var slot = item.Reserve(context.Owner);
            if (slot < 0)
            {
                continue;
            }

            var errand = new Errand(item, use, slot);
            if (Plan(context, errand) is not null)
            {
                context.Errand = errand;
                return true;
            }

            item.Release(context.Owner);
            context.Unreachable[item.Id] = context.Time + context.Config.Furniture.RetrySeconds;
        }

        return false;
    }

    /// <summary>Where the pet should stand to use the furniture (feet x on the furniture's surface), or null if it can't.</summary>
    public static double? TargetX(PetContext context, Errand errand)
    {
        var item = errand.Item;
        if (item.GroundKey is not { } key)
        {
            return null;
        }

        if (errand.Use.Effect == UseEffect.Kick)
        {
            return item.Feet.X; // walk up to the ball; the kick happens on arrival
        }

        var x = item.SlotX(errand.Slot);
        var segment = RoutePlanner.IndexOf(context.Platforms, key, item.Feet.X);
        if (segment < 0)
        {
            return null;
        }

        var ground = context.Platforms[segment];
        return System.Math.Clamp(x, ground.Left + 1, ground.Right - 1); // a slot past the end of a window top: as close as it gets
    }

    /// <summary>Plans (or replans) the way from where the pet stands to its slot. Null if there's no way.</summary>
    public static IReadOnlyList<RouteStep>? Plan(PetContext context, Errand errand)
    {
        var body = context.Body;
        if (body.GroundKey is not { } from || TargetX(context, errand) is not { } x || errand.Item.GroundKey is not { } to)
        {
            return null;
        }

        errand.Route = RoutePlanner.Plan(context.Platforms, context.Walls, context.Monitors, from, body.Feet.X, to, x, RouteFollower.Walker(context));
        errand.PlannedFrom = from;
        errand.PlannedTarget = x;
        errand.ReplanIn = 0.5;
        return errand.Route;
    }

    private static bool CanChoose(PetContext context) =>
        context.Pen is null && !context.OnTrip && context.Body.Mode == Physics.BodyMode.Grounded
        && context.Furniture.Items.Count > 0;

    /// <summary>Furniture uses worth going for now: free, standing somewhere, not recently found unreachable.</summary>
    private static IEnumerable<(FurnitureItem Item, FurnitureUse Use, double Score, bool Curious)> Candidates(PetContext context)
    {
        var rules = context.Config.Furniture;
        foreach (var item in context.Furniture.Items)
        {
            if (item.IsHeld || item.GroundKey is null || !item.HasFreeSlot(context.Owner)
                || (context.Unreachable.TryGetValue(item.Id, out var retryAt) && retryAt > context.Time))
            {
                continue;
            }

            foreach (var use in item.Spec.Uses)
            {
                if (use.Effect == UseEffect.Confine || (use.Need != Need.None && Value(use.Need, context.Stats) >= Full))
                {
                    continue;
                }

                if (use.Effect == UseEffect.Breed)
                {
                    if (context.MayTakePill)
                    {
                        yield return (item, use, PillAppeal, false); // tempting: pets go for a pill fairly soon
                    }

                    continue;
                }

                var urgency = Urgency(use.Need, context.Stats, rules);
                if (urgency > 0)
                {
                    yield return (item, use, urgency, false);
                }
                else if (use.Need is Need.None or Need.Happiness)
                {
                    yield return (item, use, 0.01, true); // just for fun
                }
            }
        }
    }
}
